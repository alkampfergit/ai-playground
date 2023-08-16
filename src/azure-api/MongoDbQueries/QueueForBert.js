db.getCollection('documents_to_index').updateOne(
{_id : "S:\\OneDrive\\Documentation\\HumbleBundle\\Interesting\\advancedapisecurity.pdf"}, 
{$set : {"Embedding" : ISODate('2002-01-01'), "Processing" : false, "EmbeddingModel" : "sentence-transformers/distiluse-base-multilingual-cased-v1", "EmbeddingModelKey" : "Bert"}})

db.getCollection('documents_to_index').updateOne(
{_id : "S:\\OneDrive\\Documentation\\HumbleBundle\\Interesting\\advancedapisecurity.pdf"}, 
{$set : {"Embedding" : ISODate('2002-01-01'), "Processing" : false, "EmbeddingModel" : "sentence-transformers/all-mpnet-base-v2", "EmbeddingModelKey" : "BertMpNetv2"}})


db.getCollection('documents_to_index').updateMany(
{}, 
{$set : {"Embedding" : ISODate('2002-01-01'), "Processing" : false, "EmbeddingModel" : "sentence-transformers/distiluse-base-multilingual-cased-v1", "EmbeddingModelKey" : "Bert"}})

db.getCollection('documents_to_index').updateMany(
{}, 
{$set : {"Embedding" : ISODate('2002-01-01'), "Processing" : false, "EmbeddingModel" : "sentence-transformers/all-mpnet-base-v2", "EmbeddingModelKey" : "BertMpNetv2"}})
