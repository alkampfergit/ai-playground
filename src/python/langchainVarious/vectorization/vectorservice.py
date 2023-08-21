from flask import Flask, request, jsonify
from sentence_transformers import SentenceTransformer
import os

app = Flask(__name__)

transformers_cache = os.environ.get('TRANSFORMERS_CACHE')
transformers_dict = {}

@app.route('/vectorize', methods=['POST'])
def vectorize_text():
    data = request.get_json()
    model_name = data['Model']
    text = [data['Text']]
    
    # Check if model already exists in dictionary
    if model_name in transformers_dict:
        model = transformers_dict[model_name]
    else:
        # Create new model and add to dictionary
        model = SentenceTransformer(
            model_name, 
            cache_folder=transformers_cache,
            device='cuda')
        transformers_dict[model_name] = model

    encoded = model.encode(text)

    # Now return only the first result (we have only one vector) as array
    response = {'vector': encoded[0].tolist()}
    return jsonify(response)

if __name__ == '__main__':
    app.run(port=5001)
