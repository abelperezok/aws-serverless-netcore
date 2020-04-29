using System.IO;
using System.Text;

namespace project.lambda
{
    public class Function
    {
	    public Stream HelloHandler(Stream input)
        {
            StreamReader reader = new StreamReader(input);
            var text = reader.ReadToEnd();
            return new MemoryStream(Encoding.UTF8.GetBytes($"Hello {text}!"));
        }        
    }
}
