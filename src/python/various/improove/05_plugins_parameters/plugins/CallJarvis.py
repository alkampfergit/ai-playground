from pydantic import BaseModel, Field
from langchain.tools import tool

class Tasks:
    """
    A class to handle various task-related operations such as changing task titles,
    searching for tasks, and loading task details from an API.
    """

    #https://python.langchain.com/v0.1/docs/modules/tools/custom_tools/
    
    class ChangeTaskTitleInput(BaseModel):
        task_id: str = Field(description="The id of the task to change the title")
        new_title: str = Field(description="The new title of the task")

    def change_task_title(self, task_id: str, new_title: str) -> int:
        """
        Change the title of a task
        """

        print(f"Changing title of task {task_id} to {new_title}")
        # Here you should call the API to change title
        # api will return new version of the task
        new_version = 7
        return new_version
    
    class SearchTaskInput(BaseModel):
        search_string: str = Field(description="The search string to search for a task")

    def search_task(self, search_string: str) -> str:
        """
        Search for a task
        """
        print(f"Searching for task {search_string}")
        # Here you should call the API to search for a task
        # api will return a list of tasks
        tasks = ["Task_3", "Task_4", "Task_6"]
        return tasks

    class LoadTaskInput(BaseModel):
        task_id: str = Field(description="The id of the task to load")
        
    def load_task(self, task_id: str) -> str:
        """
        Load a task from the API and return the task content in json format
        """
        print(f"Loading task {task_id}")
        # Here you should call the API to load a task
        # api will return the task content
        tasks = {
            "Task_3": {
                "id": "Task_3",
                "title": "Hey I'm task 3",
                "description": "This is the description of task 3",
                "version": 5,
                "due_date": "2021-10-10"
            },
            "Task_4": {
                "id": "Task_4",
                "title": "I'm beautiful task 4",
                "description": "This is the description of task 4",
                "version": 5,
                "due_date": "2022-10-10"
            },
            "Task_6": {
                "id": "Task_6",
                "title": "I'm the six",
                "description": "This is the description of task 6",
                "version": 5,
                "due_date": "2023-10-10"
            }
        }
        return tasks.get(task_id)
    


    class LoadTasksInput(BaseModel):
        task_ids: list[str] = Field(description="The ids of the tasks to load")

    def load_tasks(self, task_ids: list[str]) -> list[dict]:
        """
        Load a list of tasks from the API and return the tasks content in json format
        """
        print(f"Loading tasks {task_ids}")
        # Here you should call the API to load tasks
        # api will return the tasks content
        import json

        tasks = {
            "Task_3": {
                "id": "Task_3",
                "title": "Hey I'm task 3",
                "description": "This is the description of task 3",
                "version": 5,
                "due_date": "2021-10-10"
            },
            "Task_4": {
                "id": "Task_4",
                "title": "I'm beautiful task 4",
                "description": "This is the description of task 4",
                "version": 5,
                "due_date": "2022-10-10"
            },
            "Task_6": {
                "id": "Task_6",
                "title": "I'm the six",
                "description": "This is the description of task 6",
                "version": 5,
                "due_date": "2023-10-10"
            }
        }
        selected_tasks = [tasks.get(task_id) for task_id in task_ids if task_id in tasks]
        ret_value = json.dumps(selected_tasks)
        print(f"get tasks returned {ret_value}")
        return ret_value

        