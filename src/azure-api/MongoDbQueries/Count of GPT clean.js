db.getCollection('documents_to_index').aggregate([

    /* a condition if you need to find all book that contains a certain text*/
    //{$match : {"Pages.Content" : /Complete Mediation/i}},

    {$unwind : "$Pages"},
    {$addFields: {
        "Gpt": {
            $cond: {
                if: {$and: [
                    {$eq: [{$type: "$Pages.Gpt35PageInformation"}, "object"]},
                    {$ne: ["$Pages.Gpt35PageInformation", null]}
                ]},
                then: 1,
                else: 0
            }
        }
    }},

    {$group: {
        _id: "$_id",
        PageCount : {$sum : 1},
        RemovedCount : {$sum: {$cond: [{$eq: ["$Pages.Removed", true]}, 1, 0]}},
        Gpt1Count: {$sum: {$cond: [{$eq: ["$Gpt", 1]}, 1, 0]}},
        Gpt0Count: {$sum: {$cond: [{$eq: ["$Gpt", 0]}, 1, 0]}}
    }},
    {$addFields:{
        "Completed": {
            $cond:{
                if:{
                    $eq:[{$add:["$Gpt1Count","$RemovedCount"]},"$PageCount"]
                },
                then:true,
                else:false
            }
        }
    }},
    {$match:{
        "Completed":false, "PageCount" : {$gt : 10}
    }},
    {$sort : {"Gpt1Count" : -1}}
])
