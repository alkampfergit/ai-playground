# langchain book

You can find the book at [https://www.pinecone.io/learn/langchain-intro/](https://www.pinecone.io/learn/langchain-intro/) this is the folder with code and examples.

Create a local environment with python, then allow the ipykernel to create a kernel for jupyter notebooks

```bash
python3 -m venv langchain
source langchain/bin/activate

pip install ipykernel
python -m ipykernel install --user --name=langchain
```

you can handle requirements with easy thanks to pip

```bash
pip install -r requirements.txt
pip freeze > requirements.txt
```

