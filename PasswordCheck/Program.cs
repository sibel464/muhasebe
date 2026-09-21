using System;

var hash = "$2a$11$bFJTJsZZdqJQwi75rB4wM.DQgaRKmvwOSvi0VYrjuKdFj1/QC2UfS";
foreach (var value in new[] { "admin", "123456", "Admin123", "Admin123!", "admin123", "1", "1111", "1234", "muhasebe" })
    Console.WriteLine($"{value}={BCrypt.Net.BCrypt.Verify(value, hash)}");
