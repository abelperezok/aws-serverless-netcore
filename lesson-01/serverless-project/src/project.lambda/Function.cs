using System.IO;
using System.Text;

namespace project.lambda
{
    public class Function
    {
        public Stream HelloHandler()
        {
            return new MemoryStream(Encoding.UTF8.GetBytes($"Hello Lambda!"));
        }
    }
}
