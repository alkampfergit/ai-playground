db.getCollection('documents_to_index').updateMany(
{}, 
{$set : {"IndexToElastic" : ISODate('2002-01-01'), "Processing" : false}, $unset : {"Elastic" : "", "LastModification" : "", "EmbeddingKey" : ""}})