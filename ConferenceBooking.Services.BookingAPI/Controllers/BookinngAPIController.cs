
using ConferenceBooking.Services.BookingAPI.Models.Dto;
using ConferenceBooking.Services.BookingAPI.Service.IService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConferenceBooking.Services.BookingAPI.Controllers
{
    [Route("api/conference-halls")]
    [ApiController]
    public class BookingAPIController : ControllerBase
    {
        private readonly IConferenceHallService _conferenceHallService;
        private readonly IFacilityService _facilityService;
        private readonly IVCPlatformService _vcPlatformService;

        public BookingAPIController(
            IConferenceHallService conferenceHallService,
            IFacilityService facilityService,
            IVCPlatformService vcPlatformService)
        {
            _conferenceHallService = conferenceHallService;
            _facilityService = facilityService;
            _vcPlatformService = vcPlatformService;
        }

        // =========================================================
        // CONFERENCE HALL APIs
        // =========================================================

        [HttpGet]
        [Authorize(Policy = "AuthenticatedUser")]
        public async Task<IActionResult> GetAll(
            [FromQuery] bool activeOnly = true,
            CancellationToken cancellationToken = default)
        {
            var response = await _conferenceHallService.GetAll(
                activeOnly, cancellationToken);

            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("{id:int}")]
        [Authorize(Policy = "AuthenticatedUser")]
        public async Task<IActionResult> GetById(
            int id,
            CancellationToken cancellationToken = default)
        {
            var response = await _conferenceHallService.GetById(
                id, cancellationToken);

            return StatusCode(response.StatusCode, response);
        }

        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Create(
            [FromBody] CreateHallDto dto,
            CancellationToken cancellationToken = default)
        {
            var response = await _conferenceHallService.Create(
                dto, cancellationToken);

            return StatusCode(response.StatusCode, response);
        }

        [HttpPut("{id:int}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Update(
            int id,
            [FromBody] UpdateHallDto dto,
            CancellationToken cancellationToken = default)
        {
            var response = await _conferenceHallService.Update(
                id, dto, cancellationToken);

            return StatusCode(response.StatusCode, response);
        }

        // =========================================================
        // FACILITY MASTER APIs
        // =========================================================

        // GET: api/facilities
        [HttpGet("~/api/facilities")]
        [Authorize(Policy = "AuthenticatedUser")]
        public async Task<IActionResult> GetAllFacilities(
            [FromQuery] bool activeOnly = true,
            CancellationToken cancellationToken = default)
        {
            var response = await _facilityService.GetAll(
                activeOnly, cancellationToken);

            return StatusCode(response.StatusCode, response);
        }

        // GET: api/facilities/5
        [HttpGet("~/api/facilities/{id:int}")]
        [Authorize(Policy = "AuthenticatedUser")]
        public async Task<IActionResult> GetFacilityById(
            int id,
            CancellationToken cancellationToken = default)
        {
            var response = await _facilityService.GetById(
                id, cancellationToken);

            return StatusCode(response.StatusCode, response);
        }

        // POST: api/facilities
        [HttpPost("~/api/facilities")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> CreateFacility(
            [FromBody] CreateFacilityDto dto,
            CancellationToken cancellationToken = default)
        {
            var response = await _facilityService.Create(
                dto, cancellationToken);

            return StatusCode(response.StatusCode, response);
        }

        // PUT: api/facilities/5
        [HttpPut("~/api/facilities/{id:int}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> UpdateFacility(
            int id,
            [FromBody] UpdateFacilityDto dto,
            CancellationToken cancellationToken = default)
        {
            var response = await _facilityService.Update(
                id, dto, cancellationToken);

            return StatusCode(response.StatusCode, response);
        }

        // =========================================================
        // HALL-FACILITY MAPPING APIs
        // =========================================================

        // GET: api/conference-halls/5/facilities
        [HttpGet("{hallId:int}/facilities")]
        [Authorize(Policy = "AuthenticatedUser")]
        public async Task<IActionResult> GetHallFacilities(
            int hallId,
            [FromQuery] bool availableOnly = true,
            CancellationToken cancellationToken = default)
        {
            var response = await _facilityService.GetHallFacilities(
                hallId, availableOnly, cancellationToken);

            return StatusCode(response.StatusCode, response);
        }

        // POST: api/conference-halls/5/facilities
        [HttpPost("{hallId:int}/facilities")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> MapFacilityToHall(
            int hallId,
            [FromBody] HallFacilitySelectionDto dto,
            CancellationToken cancellationToken = default)
        {
            var response = await _facilityService.MapFacilityToHall(
                hallId, dto, cancellationToken);

            return StatusCode(response.StatusCode, response);
        }

        // DELETE: api/conference-halls/5/facilities/3
        [HttpDelete("{hallId:int}/facilities/{facilityId:int}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> UnmapFacilityFromHall(
            int hallId,
            int facilityId,
            CancellationToken cancellationToken = default)
        {
            var response = await _facilityService.UnmapFacilityFromHall(
                hallId, facilityId, cancellationToken);

            return StatusCode(response.StatusCode, response);
        }

        // =========================================================
        // VC PLATFORM MASTER APIs
        // =========================================================

        // GET: api/vc-platforms
        [HttpGet("~/api/vc-platforms")]
        [Authorize(Policy = "AuthenticatedUser")]
        public async Task<IActionResult> GetAllVCPlatforms(
            [FromQuery] bool activeOnly = true,
            CancellationToken cancellationToken = default)
        {
            var response = await _vcPlatformService.GetAll(
                activeOnly, cancellationToken);

            return StatusCode(response.StatusCode, response);
        }

        // GET: api/vc-platforms/5
        [HttpGet("~/api/vc-platforms/{id:int}")]
        [Authorize(Policy = "AuthenticatedUser")]
        public async Task<IActionResult> GetVCPlatformById(
            int id,
            CancellationToken cancellationToken = default)
        {
            var response = await _vcPlatformService.GetById(
                id, cancellationToken);

            return StatusCode(response.StatusCode, response);
        }

        // POST: api/vc-platforms
        [HttpPost("~/api/vc-platforms")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> CreateVCPlatform(
            [FromBody] CreateVCPlatformDto dto,
            CancellationToken cancellationToken = default)
        {
            var response = await _vcPlatformService.Create(
                dto, cancellationToken);

            return StatusCode(response.StatusCode, response);
        }

        // PUT: api/vc-platforms/5
        [HttpPut("~/api/vc-platforms/{id:int}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> UpdateVCPlatform(
            int id,
            [FromBody] UpdateVCPlatformDto dto,
            CancellationToken cancellationToken = default)
        {
            var response = await _vcPlatformService.Update(
                id, dto, cancellationToken);

            return StatusCode(response.StatusCode, response);
        }

        // =========================================================
        // HALL-VC PLATFORM MAPPING APIs
        // =========================================================

        // GET: api/conference-halls/5/vc-platforms
        [HttpGet("{hallId:int}/vc-platforms")]
        [Authorize(Policy = "AuthenticatedUser")]
        public async Task<IActionResult> GetHallVCPlatforms(
            int hallId,
            [FromQuery] bool activeOnly = true,
            CancellationToken cancellationToken = default)
        {
            var response = await _vcPlatformService.GetHallVCPlatforms(
                hallId, activeOnly, cancellationToken);

            return StatusCode(response.StatusCode, response);
        }

        // POST: api/conference-halls/5/vc-platforms
        [HttpPost("{hallId:int}/vc-platforms")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> MapVCPlatformToHall(
            int hallId,
            [FromBody] HallVCPlatformSelectionDto dto,
            CancellationToken cancellationToken = default)
        {
            var response = await _vcPlatformService.MapVCPlatformToHall(
                hallId, dto, cancellationToken);

            return StatusCode(response.StatusCode, response);
        }

        // DELETE: api/conference-halls/5/vc-platforms/3
        [HttpDelete("{hallId:int}/vc-platforms/{vcPlatformId:int}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> UnmapVCPlatformFromHall(
            int hallId,
            int vcPlatformId,
            CancellationToken cancellationToken = default)
        {
            var response = await _vcPlatformService.UnmapVCPlatformFromHall(
                hallId, vcPlatformId, cancellationToken);

            return StatusCode(response.StatusCode, response);
        }
    }
}
