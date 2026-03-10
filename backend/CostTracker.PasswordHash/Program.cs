using Microsoft.AspNetCore.Identity;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: dotnet run --project backend/CostTracker.PasswordHash -- <username> <password>");
    return 1;
}

var username = args[0];
var password = args[1];
var passwordHasher = new PasswordHasher<string>();

Console.WriteLine(passwordHasher.HashPassword(username, password));
return 0;
