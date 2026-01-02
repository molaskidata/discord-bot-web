module.exports = {
    apps: [
        {
            name: "cmain",
            cwd: "./Bots/C-Main",
            script: "dotnet",
            args: "run",
            env: {
                ASPNETCORE_ENVIRONMENT: "Production"
            }
        },
        {
            name: "cping",
            cwd: "./Bots/C-Ping",
            script: "dotnet",
            args: "run",
            env: {
                ASPNETCORE_ENVIRONMENT: "Production"
            }
        },
        {
            name: "cpirat",
            cwd: "./Bots/C-Pirat",
            script: "dotnet",
            args: "run",
            env: {
                ASPNETCORE_ENVIRONMENT: "Production"
            }
        },
        {
            name: "cweb",
            cwd: "./Website/WebsiteServer",
            script: "dotnet",
            args: "run",
            env: {
                ASPNETCORE_ENVIRONMENT: "Production",
                ASPNETCORE_URLS: "http://localhost:5000"
            }
        }
    ]
};