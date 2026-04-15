namespace Infrastructure.Email;

public class EmailSettings
{
    public string Host { get; set; } = "smtp.1and1.com";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string FromAddress { get; set; } = "support@aivante.com";
    public string FromDisplayName { get; set; } = "AiVante";
    public string Password { get; set; } = "";
    public int TimeoutMs { get; set; } = 20000;
    public string FrontendBaseUrl { get; set; } = "http://localhost:4200";
}