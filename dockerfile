################
#  Build stage
################

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build

# Copy csproj and restore as distinct layers
WORKDIR /app
COPY src/*.csproj ./src/
WORKDIR /app/src
RUN dotnet restore

# Build the application
COPY src/ /app/src/
RUN dotnet publish -c Release -o /app/publish --no-restore


################
# Runtime stage
################
FROM mcr.microsoft.com/dotnet/runtime:5.0

# The dotnet runtime is based on Buster. Debian have since moved the apt sources.
RUN sed -i s/deb.debian.org/archive.debian.org/g /etc/apt/sources.list && \
    sed -i s/security.debian.org/archive.debian.org/g /etc/apt/sources.list

# libfontconfig1 is silently required by SkiaSharp on Linux.
RUN apt-get update && apt-get install -y libfontconfig1

WORKDIR /app
COPY --from=build /app/publish ./
# The data directory is not required for the build, but is for the exe. The exe
# accepts the path to the data directory as the first argument, or uses the
# working directory by default. So let's put it in the working directory.
COPY data ./data

# Document the GigE Vision ports. Not actually required by docker because we access
# the container directly by the Docker subnet instead of indirectly via the host.
#   GVCP (GigE Vision Control Protocol)
EXPOSE 3956/udp
#   GVSP (GigE Vision Stream Protocol)
EXPOSE 20202-20203/udp
#   Widely reported as the dynamic GVSP port. I think that's mistaking the host-side port.
#EXPOSE 49152-65536/udp

# Run
ENV DOTNET_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "GigE-Cam-Simulator.dll"]
