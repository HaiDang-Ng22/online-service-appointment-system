# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY ["Appointment.csproj", "./"]
RUN dotnet restore "Appointment.csproj"

# Copy full source code and build
COPY . .
RUN dotnet publish "Appointment.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install fontconfig and libgdiplus for PDF & QR Code rendering
RUN apt-get update && apt-get install -y --no-install-recommends \
    fontconfig \
    libgdiplus \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Expose HTTP port
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "Appointment.dll"]
