FROM mcr.microsoft.com/dotnet/sdk:10.0.402 AS build
WORKDIR /src
COPY pc/Directory.Build.props pc/Directory.Build.props
COPY pc/NuGet.config pc/NuGet.config
COPY pc/src/ChildScreenTime.Core/ pc/src/ChildScreenTime.Core/
COPY pc/src/ChildScreenTime.Remote/ pc/src/ChildScreenTime.Remote/
RUN dotnet restore pc/src/ChildScreenTime.Remote/ChildScreenTime.Remote.csproj --configfile pc/NuGet.config --locked-mode
RUN dotnet publish pc/src/ChildScreenTime.Remote/ChildScreenTime.Remote.csproj -c Release --no-restore -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.13 AS runtime
WORKDIR /app
COPY --from=build /app .
RUN mkdir -p /var/lib/child-screen-time/keys && chown -R $APP_UID:$APP_UID /var/lib/child-screen-time
USER $APP_UID
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ChildScreenTime.Remote.dll"]
