using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace HttpWebRequestWrapper.Tests.Properties
{
    public static class Extensions
    {
        public static List<Cookie> ToList(this CookieCollection cookies)
        {
            var cookieArray = new Cookie[cookies.Count];
            cookies.CopyTo(cookieArray, 0);

            return cookieArray.ToList();
        }
    }
}
