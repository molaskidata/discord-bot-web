module.exports = {
    apps: [
        {
            name: "cmain",
            cwd: "./Bots/C-Main",
            script: "/bin/bash",
            args: ["-c", "export $(cat .env | xargs) && dotnet run"],
            env: {
                ASPNETCORE_ENVIRONMENT: "Production"
            }
        },
        {
            name: "cping",
            cwd: "./Bots/C-Ping",
            script: "/bin/bash",
            args: ["-c", "export $(cat .env | xargs) && dotnet run"],
            env: {
                ASPNETCORE_ENVIRONMENT: "Production"
            }
        },
        {
            name: "cpirat",
            cwd: "./Bots/C-Pirat",
            script: "/bin/bash",
            args: ["-c", "export $(cat .env | xargs) && dotnet run"],
            env: {
                ASPNETCORE_ENVIRONMENT: "Production"
            }
        },
        {
            name: "cweb",
            cwd: "./Website/WebsiteServer",
            script: "/bin/bash",
            args: ["-c", "dotnet run"],
            env: {
                ASPNETCORE_ENVIRONMENT: "Production",
                ASPNETCORE_URLS: "http://localhost:5000"
            }
        }
    ]
};