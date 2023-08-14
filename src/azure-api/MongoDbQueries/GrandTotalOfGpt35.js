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
        _id: null,
        Gpt1Total: {$sum: {$cond: [{$eq: ["$Gpt", 1]}, 1, 0]}},
        Gpt0Total: {$sum: {$cond: [{$eq: ["$Gpt", 0]}, 1, 0]}}
    }}
])
