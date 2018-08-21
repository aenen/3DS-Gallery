using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace _3dsGallery.WebUI.Code
{
    [AttributeUsage(AttributeTargets.Method)]
    public class Only3DSAttribute: ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if(!filterContext.HttpContext.Request.UserAgent.Contains("Nintendo 3DS"))
            {
                filterContext.Result = new RedirectResult("/Not3ds");
            }
            base.OnActionExecuting(filterContext);
        }
    }
}