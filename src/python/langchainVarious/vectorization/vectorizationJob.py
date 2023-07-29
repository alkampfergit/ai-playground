from pymongo import MongoClient, ReturnDocument
from datetime import datetime, timedelta
import asyncio
import logging
import io
import sys
import time
from sentence_transformers import SentenceTransformer

# Get MongoDB connection string from command line argument
if len(sys.argv) > 1:
    connection_string = sys.argv[1]
else:
    connection_string = "mongodb://localhost/AiDocuments"

# Connect to MongoDB
client = MongoClient(connection_string)
db = client.get_database()

# Get a reference to a collection
documents_to_index = db.get_collection("documents_to_index")

model = SentenceTransformer('sentence-transformers/distiluse-base-multilingual-cased-v1')

# Poll documents_to_index collection for documents with Bert property less than current timestamp
while True:
    
    utc_now = datetime.utcnow()
    ten_minutes_from_now = utc_now + timedelta(minutes=10)
    filter_query = {'$and': [{'BertEmbedding': {'$lt': utc_now}}, {'Processing': {'$ne': True}}]}
    update_query = {'$set': {'BertEmbedding': ten_minutes_from_now, 'Processing': True}}

    # execute the query and process file until no documents is returned anymore
    while True:
        raw_document = documents_to_index.find_one_and_update(
            filter_query, 
            update_query,
            return_document=ReturnDocument.AFTER
        )
        
        if not raw_document:
            break

        try:

            # Get all the patges that are not removed
            pages = [page for page in raw_document['Pages'] if not page['Removed']]
            
            # Now build a vector with the same dimension of the pages, then iterate over
            # pages and if we have a property Gpt35PageInformation we will put the 
            # Gpt35PageInformation.CleanText property value inside the array if not 
            # add the Content property
            pages_vector = []
            for page in pages:
                hasGptClean = 'Gpt35PageInformation' in page and 'CleanText' in page['Gpt35PageInformation']
                if hasGptClean:
                    pages_vector.append(page['Gpt35PageInformation']['CleanText'])
                else:
                    pages_vector.append(page['Content'])


            print(len(pages_vector))
        except Exception as ex:
            logging.error(f"Error occurred while vectorizing document: {ex}")

    # Wait for 2 second before polling again
    time.sleep(2)
