FROM mcr.microsoft.com/vscode/devcontainers/dotnet:0.201.9-5.0

# Install latest .NET Core 3.1 SDK
RUN wget https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb && \
	sudo dpkg -i packages-microsoft-prod.deb && \
	sudo apt-get update && \
	sudo apt-get install -y apt-transport-https && \
	sudo apt-get update && \
	sudo apt-get install -y dotnet-sdk-3.1

# Built with ❤ by [Pipeline Foundation](https://pipeline.foundation)