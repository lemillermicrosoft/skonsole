# SKonsole - A Console App built on Semantic Plugins
Welcome to the SKonsole repository! SKonsole is a powerful command-line tool that leverages AI to assist you with various tasks. It provides a simple interface to interact with the AI model and perform operations like reading and writing files, searching for files, and even sending emails. The repository contains the source code for the SKonsole application and its plugins.

## Features

- AI-powered command-line interface
- Customizable configuration
- Generate commit messages, pull request descriptions, and feedback.
- Engage in chat conversations that are powered by various Plugins.

## Usage

### General Commands

These commands will execute and return a result from the LLM.

`skonsole commit <commitHash>`: Generate commit messages based on the provided commit hash.

`skonsole pr feedback`: Generate valuable feedback for pull requests using git diff or git show output.

`skonsole pr description`: Generate detailed descriptions for pull requests using git diff or git show output.

### Chat Commands

These commands will start a chat conversation with the LLM.

`skonsole stepwise [options]`: Engage in a StepwisePlanner powered chat session. Use `optionSet` option to specify which optionSets should be used for planning.

`skonsole createPlan <message>`: Create plans using a Planner by providing a message and then execute the plan.

`skonsole promptChat`: Engage in interactive prompt chat sessions for building semantic prompts using the LLM.

### Other Commands

These commands are other utilities that do not directly leverage LLMs.

`skonsole config [command] [options]`: Configure SKonsole application settings like LLM endpoints, keys, etc.

## Configuration

You can customize the behavior of SKonsole by modifying the configuration settings. In addition to the `config` command, the configuration file is located at `.skonsole` in your user profile directory. You can also set environment variables to override the default settings.

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

## Plugins

The repository includes the following plugins:

### CondensePlugin

The CondensePlugin is designed to help condense text by using the LLM to merge multiple chunks of text.

### PRPlugin

The PRPlugin is designed to help generate pull request summaries and change lists from `git diff` output.

### SuperFileIOPlugin

The SuperFileIOPlugin is an extension of the FileIOPlugin in Semantic Kernel. It includes additional capabilities for reading and writing from the file system.


## Contributing
See [Contributing](CONTRIBUTING.md).

## License

SKonsole is licensed under the MIT License.

*Powered by [Microsoft Semantic Kernel](https://github.com/microsoft/semantic-kernel)*