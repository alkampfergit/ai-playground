# How to use local environment 

Create a local environment with python, then allow the ipykernel to create a kernel for jupyter notebooks

```bash
python3 -m venv myenv
source myenv/bin/activate
```

Now install all pip modules you need

```bash
pip install openai
pip install langchain
pip install matplotlib
pip install plotly
```

Actually you can install everything with.

```bash
pip install -r requirements.txt
```

If you install other packages update requirements.txt with

```bash
pip freeze > requirements.txt
```

Then I want to use the current environment as kernel for jupyter notebook

```bash
pip install ipykernel
python -m ipykernel install --user --name=<kernel_name>
jupyter notebook
```

Now if you want to use the very same kernel with Visual Studio Code you need to select the right python environment 

- Open Visual Studio Code and connect to your Codespace.
- Open the command palette by pressing Ctrl+Shift+P (Windows/Linux) or Cmd+Shift+P (macOS).
- Type "Python: Select Interpreter" and select it from the list of options.
- In the list of available interpreters, select the one that corresponds to the virtual environment you created earlier. It should be located in the myenv/bin directory.

## Openai change for azure

First you need to create the correct api key you can write in a .env file

```bash
OPENAI_API_KEY=xxxxxx
OPENAI_API_BASE=https://alkopenai.openai.azure.com/
```

