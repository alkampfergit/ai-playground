db.getCollection('documents_to_index').updateMany(
{
     "Pages.Gpt35PageInformation.CleanText" : {$exists : 0}
    //"_id" : "S:\\OneDrive\\Documentation\\HumbleBundle\\Interesting\\advancedapisecurity.pdf"
  },
  { 
    $unset : {"Processing" : ""},
    $set : { "CleanWithGpt35" : ISODate("2023-08-01T12:26:53.946Z")} 
})


// to remove everything from the queue
//db.getCollection('documents_to_index').updateMany({}, {$unset : {"CleanWithGpt35" : "","Processing" : "" }})
