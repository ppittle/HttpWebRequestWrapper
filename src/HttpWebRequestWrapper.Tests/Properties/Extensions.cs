﻿using System.Collections.Generic;
using System.Linq;
using System.Net;

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
