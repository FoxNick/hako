namespace HakoJS.Exceptions;

public class HakoException : Exception
{
    public HakoException(string message) : base(message)
    {
    }

    public HakoException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
    
    public override string Message
    {
        get
        {
            if (InnerException == null)
                return base.Message;
            
            if (InnerException is JavaScriptException jsEx)
            {
                // Format JavaScript exceptions nicely
                var jsError = !string.IsNullOrEmpty(jsEx.JsErrorName) && !string.IsNullOrEmpty(jsEx.JsMessage)
                    ? $"{jsEx.JsErrorName}: {jsEx.JsMessage}"
                    : jsEx.JsMessage ?? jsEx.Message;
                
                return $"{base.Message} --> {jsError}";
            }
            
            return $"{base.Message} --> {InnerException.Message}";
        }
    }
}