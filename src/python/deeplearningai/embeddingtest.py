import os
import openai
from langchain.chat_models import AzureChatOpenAI
from dotenv import load_dotenv, find_dotenv
from langchain.prompts import ChatPromptTemplate
from pprint import pprint
# from openai.embeddings_utils import get_embedding
import argparse

def get_embedding(text: str, model="text-embedding-ada-002"):
    """
    Get the embedding of a text using OpenAI's API.

    Args:
        text (str): The text to embed.
        model (str): The name of the model to use for embedding.

    Returns:
        An OpenAI embedding object.
    """
    # Replace newlines with spaces
    text = text.replace("\n", " ")
    # Create an embedding using the specified model
    return openai.Embedding.create(
        input=[text],
        model=model,
        engine=model)


parser = argparse.ArgumentParser()
parser.add_argument('-c', '--content', help='Content of the book')
args = parser.parse_args()

_ = load_dotenv(find_dotenv())  # read local .env file

openai.api_type = "azure"
openai.api_version = "2023-03-15-preview"

# Remember that you need to set the OPENAI_API_BASE to point openai to your specific deployment.
openai.api_base = os.getenv('OPENAI_API_BASE')
openai.api_key = os.getenv("OPENAI_API_KEY")

embed = get_embedding(args.content, model='text-embedding-ada-002')
print(type(embed))
# to print remove some stuff
embed['data'][0]['embedding'] = embed['data'][0]['embedding'][:10]
pprint(embed)
