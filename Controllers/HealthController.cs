using System;
using System.Reflection;
using System.Web.Http;
using System.Web.Http.Cors;

namespace SinerfinGatewayCLR.Controllers
{
    /// <summary>
    /// GET /api/health
    /// Endpoint de salud para que Instana y el load balancer validen
    /// que la instancia CLR esta activa.
    /// </summary>
    [EnableCors(origins: "*", headers: "*", methods: "*")]
    [RoutePrefix("api/health")]
    public class HealthController : ApiController
    {
        [HttpGet, Route("")]
        public IHttpActionResult Get()
        {
            return Ok(new
            {
                status    = "ok",
                service   = "sinerfin-gateway-clr",
                runtime   = "netframework48",
                version   = Assembly.GetExecutingAssembly()
                                    .GetName().Version.ToString(),
                timestamp = DateTime.UtcNow
            });
        }
    }
}
