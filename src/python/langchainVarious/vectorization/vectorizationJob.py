from pymongo import MongoClient, ReturnDocument
from datetime import datetime, timedelta
import argparse
import asyncio
import logging
import os
import sys
import sklearn
import time
import traceback
from sentence_transformers import SentenceTransformer

logging.basicConfig(level=logging.INFO)

parser = argparse.ArgumentParser(description='Process command line arguments.')
parser.add_argument('--connection', type=str, help='MongoDB connection string')
# we take model and modelKey from document
# parser.add_argument('--model', type=str, help='Name of the model to use')
# parser.add_argument('--modelkey', type=str, help='Key of the model to use in mongodb document')
args = parser.parse_args()

if args.connection:
    connection_string = args.connection
else:
    connection_string = "mongodb://admin:123456##@mongo01.codewrecks.com/AiDocuments?authSource=admin"

# if args.model:
#     modelName = args.model
# else:
#     modelName = 'sentence-transformers/all-mpnet-base-v2'
#     # modelName = 'sentence-transformers/distiluse-base-multilingual-cased-v1'

# if args.modelkey:
#     modelKey = args.modelkey
# else:
#     modelKey = 'BertMpnetBaseV2'
#     # modelKey = 'Bert'

# Connect to MongoDB
client = MongoClient(connection_string)
db = client.get_database()

transformers_cache = os.environ.get('TRANSFORMERS_CACHE')

# Get a reference to a collection
documents_to_index = db.get_collection("documents_to_index")

# Get a reference to the embedding collection
embeddings = db.get_collection("documents_embeddings")

# Poll documents_to_index collection for documents with Bert property less than current timestamp
while True:
    
    utc_now = datetime.utcnow()
    ten_minutes_from_now = utc_now + timedelta(minutes=10)
    filter_query = {'$and': [{'Embedding': {'$lt': utc_now}}, {'Processing': {'$ne': True}}]}
    update_query = {'$set': {'Embedding': ten_minutes_from_now, 'Processing': True}}

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

            modelName = raw_document['EmbeddingModel']
            modelKey = raw_document['EmbeddingModelKey']
            model = SentenceTransformer(
                modelName, 
                cache_folder=transformers_cache,
                device='cuda')
            # Get all the patges that are not removed
            pages = [page for page in raw_document['Pages'] if not page['Removed']]
            
            # Now build a vector with the same dimension of the pages, then iterate over
            # pages and if we have a property Gpt35PageInformation we will put the 
            # Gpt35PageInformation.CleanText property value inside the array if not 
            # add the Content property
            pages_vector = []
            pages_gpt35 = []
            missing_gpt35_indexes = []

            num_pages = len(pages)
            num_pages_gpt35 = 0
            for i in range(num_pages):
                page = pages[i]
                pages_vector.append(page['Content'])
                
                hasGptClean = (
                    'Gpt35PageInformation' in page and 
                    page['Gpt35PageInformation'] is not None and 
                    'CleanText' in page['Gpt35PageInformation'] and 
                    page['Gpt35PageInformation']['CleanText'] is not None
                )
                
                if hasGptClean:
                    pages_gpt35.append(page['Gpt35PageInformation']['CleanText']) 
                    num_pages_gpt35 += 1
                else:
                    pages_gpt35.append('') 
                    missing_gpt35_indexes.append(page["Number"])              

            # Print the number of pages being vectorized

            print(f"Vectorizing {num_pages} pages and {num_pages_gpt35} pages cleaned with GPT35.")

            # Vectorize the pages in batch.
            pages_vector = model.encode(pages_vector)
            pages_gpt35 = model.encode(pages_gpt35)

            pages_vector_normalized  = sklearn.preprocessing.normalize(pages_vector)
            pages_gpt35_normalized = sklearn.preprocessing.normalize(pages_gpt35)

            # Now save all the embeddings in embeddings collection
            # First remove all the embeddings for the same document
            embeddings.delete_many({'DocumentId': raw_document['_id'], 'Model': modelKey})

            for i in range(num_pages):
                
                page = pages[i]
                
                # No need to add vectorization for removed pages.
                if (page["Removed"]):
                    continue

                embedding = {
                    'DocumentId': raw_document['_id'],
                    'PageNumber': i,
                    'Vector': pages_vector[i].tolist(),
                    'VectorNormalized': pages_vector_normalized[i].tolist(),
                    'Model': modelKey,
                    'CreatedOn': datetime.utcnow()
                }
                
                # Insert the GPT35 vector only if the page was really processed with GPT35
                if (page["Number"] not in missing_gpt35_indexes):
                    embedding['VectorGpt35'] = pages_gpt35[i].tolist()
                    embedding['VectorGpt35Normalized'] = pages_gpt35_normalized[i].tolist()

                embeddings.insert_one(embedding)

            logging.info(f"Vectorization completed for document {raw_document['_id']}")

            # Update the document removing the job
            documents_to_index.update_one(
                {'_id': raw_document['_id']},
                {'$set': {'Processing': False, 'IndexToElastic' : datetime.utcnow()},
                 '$unset': {'Embedding': "", 'EmbeddingModel': "", 'EmbeddingModelKey': ""}}
            )
            logging.info(f"Document {raw_document['_id']} updated")
            
        except Exception as ex:
            logging.error(f"Error occurred while vectorizing document: {ex}")
            logging.error(traceback.format_exc())

    # Wait for 2 second before polling again
    time.sleep(2)
