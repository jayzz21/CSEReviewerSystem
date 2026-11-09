namespace CseReviewer;
public class AuthResponse {
    public int Status { get; set; }
    public string Error { get; set; } = "";
    public bool MustChangePassword { get; set; }
    public string Message { get; set; } = "";
}
