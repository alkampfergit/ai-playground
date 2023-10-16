# How to run the project

## How the software is configured

As for any .NET project you can simply configure everything into the `appsettings.json` file. To avoid secrets to be committed 
you can simply place a file outside the git folder called azure-ai.json and that file will override any setting in the `appsettings.json` file.

## Tika

Just download tika, place in a folder inside your installation and then specify the path in the `appsettings.json` file.

You need to specify also the javabin path, the path to java.exe that is used to run tika.jar file.

## Atlas

Create a project in atlas, then create a user and finally obtain a connection string to connect to the database.  Place
the connection inside the azure-ai.json file (or in the appsettings.json file) and you are ready to go.