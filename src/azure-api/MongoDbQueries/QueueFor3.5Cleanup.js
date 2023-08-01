db.getCollection('documents_to_index').updateOne({_id :"S:\\OneDrive\\Documentation\\HumbleBundle\\Interesting\\blackhatpython.pdf"}, { 
    $unset : {"Processing" : ""},
$set : { "CleanWithGpt35" : ISODate("2023-08-01T12:26:53.946Z")} 
})