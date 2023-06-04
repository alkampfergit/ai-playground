# How to use local environment 

Create a local environment with python, then allow the ipykernel to create a kernel for jupyter notebooks

```bash
python3 -m venv myenv
source myenv/bin/activate

pip install ipykernel
python -m ipykernel install --user --name=<kernel_name>
jupyter notebook
```

Now if you want to use the very same kernel with Visual Studio Code you need to select the right python environment 

- Open Visual Studio Code and connect to your Codespace.
- Open the command palette by pressing Ctrl+Shift+P (Windows/Linux) or Cmd+Shift+P (macOS).
- Type "Python: Select Interpreter" and select it from the list of options.
- In the list of available interpreters, select the one that corresponds to the virtual environment you created earlier. It should be located in the myenv/bin directory.

