FROM mcr.microsoft.com/dotnet/core/sdk:2.2
ENV ASPNETCORE_ENVIRONMENT=Development
ENV BUILD_CONFIGURATION=Debug
ENV DOTNET_USE_POLLING_FILE_WATCHER=true
EXPOSE 5000

# install ClrMD dependency libdl.so
RUN apt-get update
RUN apt-get install -y libc6-dev

WORKDIR /src
COPY ["Samples/ContainerWebApp/ContainerWebApp.csproj", "./Samples/ContainerWebApp/"]
COPY ["Microsoft.Diagnostics.Runtime/Microsoft.Diagnostics.Runtime.csproj", "./Microsoft.Diagnostics.Runtime/"]

WORKDIR /src/Samples/ContainerWebApp/
RUN dotnet restore

COPY ["Samples/ContainerWebApp/.", "/src/Samples/ContainerWebApp/"]
COPY ["Microsoft.Diagnostics.Runtime/.", "/src/Microsoft.Diagnostics.Runtime/"]

RUN dotnet build --no-restore -c $BUILD_CONFIGURATION

RUN echo "exec dotnet run --no-build --no-launch-profile -c $BUILD_CONFIGURATION -- \"\$@\"" > /entrypoint.sh

ENTRYPOINT ["/bin/bash", "/entrypoint.sh"]