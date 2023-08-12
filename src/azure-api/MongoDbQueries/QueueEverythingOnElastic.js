db.getCollection('documents_to_index').updateMany(
{
    "Pages.Gpt35PageInformation.CleanText" : {$exists : 1}
    }, 
{$set : {"IndexToElastic" : ISODate('2002-01-01'), "Processing" : false}, $unset : {"Elastic" : "", "LastModification" : "", "EmbeddingKey" : ""}})