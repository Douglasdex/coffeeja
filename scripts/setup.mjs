// Prepara o ambiente local: configuração de desenvolvimento, pacotes, restore .NET e checagem do SQL Server LocalDB.
import { copyFileSync, existsSync } from "node:fs";
import { execSync } from "node:child_process";

const dir = "apps/api/src/CoffeeJa.Api";
const alvo = `${dir}/appsettings.Development.json`;
if (!existsSync(alvo)) { copyFileSync(`${dir}/appsettings.Development.example.json`, alvo); console.log("✔ appsettings.Development.json criado"); }
else console.log("• appsettings.Development.json já existe");

const run = (c) => execSync(c, { stdio: "inherit" });
run("dotnet restore apps/api");
run("dotnet tool restore || dotnet tool install --global dotnet-ef");

try { execSync("sqllocaldb info MSSQLLocalDB", { stdio: "pipe" }); console.log("✔ SQL Server LocalDB encontrado"); }
catch { console.warn("⚠ LocalDB não encontrado. Instale o SQL Server Express/LocalDB ou ajuste ConnectionStrings:Default em appsettings.Development.json"); }
console.log("\nPronto. Rode: npm run dev:all");
