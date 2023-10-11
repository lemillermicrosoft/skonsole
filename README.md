
# SKonsole - A Console App built on Semantic Plugins
This repository contains a demo of a console application called SKonsole, which uses the Semantic Kernel to run various plugins. The app currently supports two plugins: generating commit messages and generating pull request feedback. The app uses environment variables to configure the Azure OpenAI backend.

## Getting Started
To get started, simply run the following commands in the terminal:

```Copy code
apps\SKonsole> dotnet build
apps\SKonsole> dotnet run
```
This should build and run the SKonsole app. Note that you may need to configure your environment variables with your Azure OpenAI credentials before running the app.

## Projects and Classes
This repository contains several projects and classes, including:

- PRPlugin: A plugin that can generate feedback, commit messages, and pull request descriptions based on git diff or git show output. The PRPlugin uses the CondensePlugin as a dependency and implements chunking and aggregation mechanisms to handle large inputs.

- CondensePlugin: A plugin built on the Semantic Kernel that can condense multiple chunks of text into a single chunk. The plugin uses a semantic function defined with prompt templates and a completion engine.

- CommitParser and CommitChunker: Two utility classes that split and parse the input based on commit and file information. These classes are useful for generating pull request descriptions and feedback based on the git diff output.

## Installing SKonsole Tool

Install the SKonsole Tool globally with a few quick steps:
### Installation

1. Open your terminal or command prompt.
2. Run the following command:

   ```shell
   dotnet tool install --global SKonsole
   ```
3. To confirm the installation was successful, run:
   ```shell
   skonsole --version
   ```

## Available Commands

- `skonsole commit <commitHash>`: Generate commit messages based on the provided commit hash.

- `skonsole pr feedback`: Generate valuable feedback for pull requests using git diff or git show output.

- `skonsole pr description`: Generate detailed descriptions for pull requests using git diff or git show output.

- `skonsole createPlan <message>`: Create plans using the Planner subcommand by providing a message.

- `skonsole promptChat`: Engage in interactive prompt chat sessions.

## Dependencies
This project requires the following dependencies:

- [Semantic Kernel](https://github.com/microsoft/semantic-kernel)

## Future Work
In the future, the SKonsole app could be expanded to support more plugins and to parse arguments on launch. Additionally, the repository could include instructions for setting up NuGet credentials and using a GitHub Package source.

I hope this README is helpful for you and others who may use your repository in the future. Let me know if there's anything else I can do to help!


####_This README was generated using Semantic Kernel_