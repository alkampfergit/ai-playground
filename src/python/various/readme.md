# langchain experiments

Some of the files are taken from Pinecone site, on langchain-intro: You can find the book at [https://www.pinecone.io/learn/langchain-intro/](https://www.pinecone.io/learn/langchain-intro/).

## Installation

Create a local environment with python, then allow the ipykernel to create a kernel for jupyter notebooks.

```bash
python3 -m venv various
source various/bin/activate
# For windows you must use the following command to activate the virtual environment
#  .\various\Scripts\activate 
```

you can handle requirements with easy thanks to pip

```bash
pip install -r requirements.txt
pip freeze > requirements.txt
```

Then you can create a kernel for jupyter notebooks using the very same environmnent

```bash
pip install ipykernel
python -m ipykernel install --user --name=various
```

Kernel can be removed using 

```bash
jupyter kernelspec remove various
```

## Troubleshooting

If you have problem with torch not having CUDA enabled try to force reinstall the correct version, please [follow instructions here](https://pytorch.org/get-started/locally/)

```bash
pip install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu118 --upgrade --force-reinstall
```

