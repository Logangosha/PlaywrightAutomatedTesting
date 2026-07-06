// SITE IS THE NAME OF THE SITE UNDER TEST, E.G., "myapp".
// ENV IS THE NAME OF THE ENVIRONMENT UNDER TEST, E.G., "dev", "qa", "staging", "prod".
// URL IS THE BASE URL OF THE SITE UNDER TEST, E.G., "https://myapp.dev.example.com".
// AUTH IS THE AUTHENTICATION CONFIGURATION FOR THE SITE UNDER TEST, E.G., "none", "manual", "auto" 
// ACTIONS IS A COMMA-SEPARATED LIST OF TEST CATEGORIES OR MODULES TO RUN, E.G., "Category=Login,Module=User"
// HEADLESS IS A BOOLEAN FLAG INDICATING WHETHER TO RUN BROWSER TESTS IN HEADLESS MODE OR NOT.
public interface IConfig
{
    string Site { get; }
    string Env { get; }
    string Url { get; }
    AuthConfig Auth { get; }
    string Actions { get; }
    bool Headless { get; }
}
