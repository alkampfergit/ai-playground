import os
import openai
from langchain.chat_models import AzureChatOpenAI
from dotenv import load_dotenv, find_dotenv
from langchain.prompts import ChatPromptTemplate
from pprint import pprint

import argparse

parser = argparse.ArgumentParser()
parser.add_argument('-c', '--content', help='Content of customer email')
args = parser.parse_args()

pprint (args)

_ = load_dotenv(find_dotenv()) # read local .env file

openai.api_type = "azure"
openai.api_version = "2023-03-15-preview"

# Remember that you need to set the OPENAI_API_BASE to point openai to your specific deployment.

openai.api_base = os.getenv('OPENAI_API_BASE')
openai.api_key = os.getenv("OPENAI_API_KEY")

# print base to verify you are pointint to the right deployment
# print(openai.api_base)

chat = AzureChatOpenAI(deployment_name="gpt35", openai_api_version="2023-03-15-preview")

template_string = """Translate the text \
that is delimited by triple backticks \
into a style that is {style}. \
text: ```{text}```
"""

prompt_template = ChatPromptTemplate.from_template(template_string)

customer_style = """American English \
in a calm and respectful tone
"""

# customer_email = """
# Arrr, I be fuming that me blender lid \
# flew off and splattered me kitchen walls \
# with smoothie! And to make matters worse, \
# the warranty don't cover the cost of \
# cleaning up me kitchen. I need yer help \
# right now, matey!
# """

customer_messages = prompt_template.format_messages(
                    style=customer_style,
                    text=args.content)

customer_response = chat(customer_messages)

print(customer_response.content)