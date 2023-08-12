db.getCollection('documents_to_index').updateOne(
{
     "Pages.Gpt35PageInformation.CleanText" : {$exists : 0}
  },
  { 
    $unset : {"Processing" : ""},
    $set : { "CleanWithGpt35" : ISODate("2023-08-01T12:26:53.946Z")} 
})

