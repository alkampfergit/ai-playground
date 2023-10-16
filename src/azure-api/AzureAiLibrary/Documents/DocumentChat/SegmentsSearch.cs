using Nest;

namespace AzureAiLibrary.Documents.DocumentChat
{
    /// <summary>
    /// Perform a search on segments
    /// </summary>
    public class SegmentsSearch
    {
        private readonly string _indexName;

        public SegmentsSearch(string indexName)
        {
            _indexName=indexName;
        }

        public IEnumerable<string>? DocId { get; set; }

        public int NumOfRecords { get; set; } = 10;

        private bool IsEmpty => DocId == null;

        internal ISearchRequest ConfigureQuery(SearchDescriptor<ElasticDocumentSegment> s)
        {
            return s
                .Index(_indexName)
                .Size(NumOfRecords)
                .Query(q => CreateQuery(q)
           );
        }

        private QueryContainer CreateQuery(QueryContainerDescriptor<ElasticDocumentSegment> q)
        {
            //ok we need to check the type of the query.
            //first of all, is the query empty?
            if (IsEmpty) return q.MatchAll();

            QueryContainer qc = q;

            List<QueryContainer> queryParts = new List<QueryContainer>();

            //ok we have some query, first of all check if we have a keyword query.
            if (DocId?.Any() == true)
            {
                queryParts.Add(q.Terms(tq => tq.Field("s_docid.nal").Terms(DocId)));
            }

            return q.Bool(bq => bq.Must(queryParts.ToArray()));
        }
    }
}
