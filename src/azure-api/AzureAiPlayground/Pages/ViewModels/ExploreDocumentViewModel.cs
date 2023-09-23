﻿using AzureAiLibrary;
using AzureAiLibrary.Configuration;
using AzureAiLibrary.Documents;
using AzureAiLibrary.Documents.Jobs;
using AzureAiLibrary.Documents.Support;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using TiktokenSharp;
using static AzureAiLibrary.Documents.ElasticSearchService;

namespace AzureAiPlayground.Pages.ViewModels;

public class ExploreDocumentViewModel
{
    private readonly TikaOutOfProcess _tikaOutOfProcess;
    private readonly IMongoCollection<SingleDocument> _docCollection;
    private readonly IMongoCollection<SingleDocumentPage> _pagesCollection;
    private readonly TikToken _tikTokTokenizer;
    private readonly IMongoCollection<DocumentSegment> _segmentCollection;

    public ExploreDocumentViewModel(
        IOptionsMonitor<DocumentsConfig> documentsConfig,
        PythonTokenizer pythonTokenizer,
        ChatClient chatClient)
    {
        var url = new MongoUrl(documentsConfig.CurrentValue.MongoUrl);
        var settings = MongoClientSettings.FromUrl(url);
        var client = new MongoClient(settings);
        var mongoDatabase = client.GetDatabase("DocSampleV1");

        _docCollection = mongoDatabase.GetCollection<SingleDocument>("SingleDocument");
        _pagesCollection = mongoDatabase.GetCollection<SingleDocumentPage>("SingleDocumentPages");
        _segmentCollection = mongoDatabase.GetCollection<DocumentSegment>("DocumentSegments");
        
        // check we can access the database reading collection 
        var count = _docCollection.CountDocuments(new BsonDocument());
        
        // Need to use Tika to extract document.
        _tikaOutOfProcess = 
            new TikaOutOfProcess(
                documentsConfig.CurrentValue.JavaBin,
                documentsConfig.CurrentValue.Tika);
        
        TikToken.PBEFileDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Files");
        _tikTokTokenizer = TikToken.GetEncoding("cl100k_base");
    }
    
    public UiSingleDocument? CurrentDocument { get; set; } 
    
    /// <summary>
    /// Uses tika to extract document metadata and document pages from a single document, it will return
    /// the original tika extracted page, containing HTML.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="docId"></param>
    /// <returns></returns>
    public async Task ExtractDocument(string path, string docId)
    {
        //try to load the document from mongo if it is not present, lets tika extract it
        //the id is docid
        var doc = _docCollection.Find(x => x.Id == docId).FirstOrDefault();
        if (doc != null)
        {
            //load all the pages from mongo
            var loadedPages = _pagesCollection.Find(x => x.SingleDocumentId == docId).ToList();
            // load all segments from mongo
            var segments = _segmentCollection.Find(x => x.SingleDocumentId == docId).ToList();
            CurrentDocument = new UiSingleDocument(doc, loadedPages, segments);
            return;
        }

        await GetDocumentFromFileWithTikaAsync(docId, path);
    }

    public async Task UpdateCountToken()
    {
        if (CurrentDocument == null) return;

        foreach (var page in CurrentDocument.Pages)
        {
            page.Page.TokenCount = _tikTokTokenizer.Encode(page.Page.Content).Count;
        }
        await SaveCurrentDocumentInMongoDb();
    }
    
    public async Task SegmentDocument()
    {
        if (CurrentDocument == null) return;

        //ok we need to segment document using pages content and using a certain amount of token.
       
        

        await SaveCurrentDocumentInMongoDb();
    }

    private async Task SaveCurrentDocumentInMongoDb()
    {
        if (CurrentDocument == null) return;
        
        await SaveDocumentAsync(CurrentDocument.Document, CurrentDocument.Pages.Select(p => p.Page));
    }

    private async Task GetDocumentFromFileWithTikaAsync(string docId, string path)
    {
        var extractedData = await _tikaOutOfProcess.GetHtmlContentAsync(path);
        if (!extractedData.Success) return; // TODO: communicate error
        var singleDocument = new SingleDocument
        {
            Id = docId,
            Metadata = new Dictionary<string, IReadOnlyCollection<string>>()
        };
        foreach (var kvp in extractedData.Metadata!)
        {
            singleDocument.Metadata.Add(kvp.Key, kvp.Value);
        }

        List<SingleDocumentPage> pages = new List<SingleDocumentPage>();
        for (var page = 0; page < extractedData.Pages!.Count; page++)
        {
            var documentPage = new SingleDocumentPage(docId, page, extractedData.Pages.ElementAt(page));
            pages.Add(documentPage);
        }

        CurrentDocument = new UiSingleDocument(singleDocument, pages, Array.Empty<DocumentSegment>());
        await SaveDocumentAsync(singleDocument, pages);
    }

    private async Task SaveDocumentAsync(SingleDocument doc, IEnumerable<SingleDocumentPage> pages)
    {
        // Save the document and all the pages replacing all previous data in mongodb
        await _docCollection.ReplaceOneAsync(
            x => x.Id == doc.Id,
            doc,
            new ReplaceOptions
            {
                IsUpsert = true
            });
        
        // Save all the pages in a single bulk operation, first delete already existing
        // pages related to this document then save in bulk all the pages
        await _pagesCollection.DeleteManyAsync(x => x.SingleDocumentId == doc.Id);
        await _pagesCollection.InsertManyAsync(pages);
    }
}

public class UiSingleDocument
{
    public UiSingleDocument(
        SingleDocument document,
        IEnumerable<SingleDocumentPage> pages,
        IEnumerable<DocumentSegment> segments)
    {
        Document = document;
        Pages = pages
            .OrderBy(p => p.PageNumber)
            .Select(p => new UiSingleDocumentPage(p))
            .ToArray();
        
        Segments = segments
            .OrderBy(p => p.PageNumber)
            .Select(p => new UiSingleDocumentSegment(p))
            .ToArray();
            
    }

    public UiSingleDocumentSegment[] Segments { get; set; }

    public SingleDocument Document { get; set; }
    
    public IReadOnlyCollection<UiSingleDocumentPage> Pages { get; set; }
}

public class UiSingleDocumentPage
{
    public UiSingleDocumentPage(SingleDocumentPage page)
    {
        Page = page;
    }

    public bool ShowDetails { get; set; } = false;

    public SingleDocumentPage Page { get; set; }
}

public class UiSingleDocumentSegment
{
    public UiSingleDocumentSegment(DocumentSegment segment)
    {
        Segment = segment;
    }

    public bool ShowDetails { get; set; } = false;

    public DocumentSegment Segment { get; set; }
}

/// <summary>
/// Represents a single document where we want to perform various analysis.
/// </summary>
public class SingleDocument
{
    /// <summary>
    /// Id is assigned from the user and is used to uniquely identify the file.
    /// </summary>
    public string Id { get; set; } = null!;

    public required Dictionary<string, IReadOnlyCollection<string>> Metadata { get; set; }
}

/// <summary>
/// represent a single page of a document.
/// </summary>
public class SingleDocumentPage
{
    public SingleDocumentPage( string singleDocumentId, int pageNumber, string content)
    {
        SingleDocumentId = singleDocumentId;
        PageNumber = pageNumber;
        Content = content;
        Id = ObjectId.GenerateNewId();
    }

    public ObjectId Id { get; set; }

    public int PageNumber { get; set; }
    public string SingleDocumentId { get; set; }

    public string Content { get; set; }

    public int? TokenCount { get; set; }
}

/// <summary>
/// Subdividing text in pages as for Tika extractino is not smart ,it is much
/// better to divide the text in chunks
/// </summary>
public class DocumentSegment
{
    public DocumentSegment( string singleDocumentId, int pageNumber, string content, int tokenCount)
    {
        Id = ObjectId.GenerateNewId();
        SingleDocumentId = singleDocumentId;
        PageNumber = pageNumber;
        Content = content;
        TokenCount = tokenCount;
    }

    public ObjectId Id { get; set; }

    /// <summary>
    /// We can maintain the information of the page the segment belongs to
    /// </summary>
    public int PageNumber { get; set; }
    
    /// <summary>
    /// Clearly we need to reference the document
    /// </summary>
    public string SingleDocumentId { get; set; }

    public string Content { get; set; }

    public int? TokenCount { get; set; }
}