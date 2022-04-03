FROM mcr.microsoft.com/dotnet/sdk:6.0 

RUN curl -fsSL https://deb.nodesource.com/setup_current.x | bash - \
    &&  apt-get update \
    &&  apt-get install -y nodejs

COPY src/RoboScapeSimulator/bin/Release/net6.0/publish/ App/
COPY src/node/index.js App/src/node/
COPY src/node/package.json App/src/node/

RUN cd /App/src/node && npm install

WORKDIR /App
ENV DOTNET_EnableDiagnostics=0
ENTRYPOINT ["dotnet", "RoboScapeSimulator.dll"]
EXPOSE 9001
