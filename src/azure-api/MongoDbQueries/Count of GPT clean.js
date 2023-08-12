db.getCollection('documents_to_index').aggregate([
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
        Gpt1Count: {$sum: {$cond: [{$eq: ["$Gpt", 1]}, 1, 0]}},
        Gpt0Count: {$sum: {$cond: [{$eq: ["$Gpt", 0]}, 1, 0]}}
    }},
    {$sort : {Gpt1Count : -1}}
])
