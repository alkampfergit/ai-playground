db.getCollection('documents_to_index').aggregate([

{$unwind : "$Pages"},
{$group : {_id : "$_id", cnt : {$sum : 1}}},
{$sort : {cnt : -1}}
])