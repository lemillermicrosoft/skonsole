
# SKonsole - A Console App built on Semantic Skills
This repository contains a demo of a console application called SKonsole, which uses the Semantic Kernel to run various skills. The app currently supports two skills: generating commit messages and generating pull request feedback. The app uses environment variables to configure the Azure OpenAI backend.

## Getting Started
To get started, simply run the following commands in the terminal:

```Copy code
apps\SKonsole> dotnet build
apps\SKonsole> dotnet run
```
This should build and run the SKonsole app. Note that you may need to configure your environment variables with your Azure OpenAI credentials before running the app.

## Projects and Classes
This repository contains several projects and classes, including:

- PRSkill: A skill that can generate feedback, commit messages, and pull request descriptions based on git diff or git show output. The PRSkill uses the CondenseSkill as a dependency and implements chunking and aggregation mechanisms to handle large inputs.

- CondenseSkill: A skill built on the Semantic Kernel that can condense multiple chunks of text into a single chunk. The skill uses a semantic function defined with prompt templates and a completion engine.

- CommitParser and CommitChunker: Two utility classes that split and parse the input based on commit and file information. These classes are useful for generating pull request descriptions and feedback based on the git diff output.

## Dependencies
This project requires the following dependencies:

- [Semantic Kernel](https://github.com/microsoft/semantic-kernel)
- [Polly](https://github.com/App-vNext/Polly)

## Future Work
In the future, the SKonsole app could be expanded to support more skills and to parse arguments on launch. Additionally, the repository could include instructions for setting up NuGet credentials and using a GitHub Package source.

I hope this README is helpful for you and others who may use your repository in the future. Let me know if there's anything else I can do to help!


######_This README was generated using Semantic Kernel_