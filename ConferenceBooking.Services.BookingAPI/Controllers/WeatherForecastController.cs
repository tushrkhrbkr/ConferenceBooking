using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConferenceBooking.Services.BookingAPI.Controllers;

[ApiController]
[Route("booking")]
[Authorize]
public class WeatherForecastController : ControllerBase
{
    
}
