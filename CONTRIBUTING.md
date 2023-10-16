# Contributing

We welcome contributions to improve SKonsole. If you have any suggestions or bug reports, please feel free to open an issue or submit a pull request.

## Structure

The repository is organized into the following main directories:

- `apps`: Contains the SKonsole application.
- `plugins`: Contains the plugins for the SKonsole application, including the CondensePlugin and PRPlugin.

## Getting Started

To get started with the SKonsole application, follow these steps:

1. Clone the repository.

### Using Visual Studio
2. Open the `skonsole.sln` solution file in Visual Studio.
3. Build and run the SKonsole application.

### Using Terminal

*apps\SKonsole*
```Copy code
dotnet build
dotnet run
```
This should build and run the SKonsole app. Note that you may need to configure your environment variables with your Azure OpenAI credentials before running the app.

## Structure

The repository is organized into the following main directories:

- `apps`: Contains the SKonsole application.
- `plugins`: Contains the plugins for the SKonsole application, including the CondensePlugin and PRPlugin.

## Projects and Classes
This repository contains several projects and classes, including:

- PRPlugin: A plugin that can generate feedback, commit messages, and pull request descriptions based on git diff or git show output. The PRPlugin uses the CondensePlugin as a dependency and implements chunking and aggregation mechanisms to handle large inputs.

- CondensePlugin: A plugin built on the Semantic Kernel that can condense multiple chunks of text into a single chunk. The plugin uses a semantic function defined with prompt templates and a completion engine.

- CommitParser and CommitChunker: Two utility classes that split and parse the input based on commit and file information. These classes are useful for generating content and responses based on large text results.

## Dependencies
This project requires the following dependencies:

- [Semantic Kernel](https://github.com/microsoft/semantic-kernel)

*Powered by [Microsoft Semantic Kernel](https://github.com/microsoft/semantic-kernel)*