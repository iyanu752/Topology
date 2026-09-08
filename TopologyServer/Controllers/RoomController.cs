using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace TopologyServer
{
    [Route("api/[controller]")]
    [ApiController]
    public class RoomController : ControllerBase
    {
        private readonly IRoomService _roomService;
        public RoomController(IRoomService roomService)
        {
            _roomService = roomService;
        }

        [HttpPost("create")]
        [Authorize]
        public async Task<IActionResult> CreateRoom(CreateRoomDto createRoomDto)
        {
            var room = _roomService.CreateRoomAsync(User, createRoomDto);
            return Ok(room);
        }

        [HttpPost("join")]
        [Authorize]
        public async Task<IActionResult> JoinRoom(JoinRoomDto joinRoomDto)
        {

            try
            {
                var room = _roomService.JoinRoomAsync(User, joinRoomDto);
                if (room == null)
                {
                    return NotFound("Room not found");
                }
                return Ok(room);

            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }

        }

        [HttpDelete]
        [Authorize]
        public async Task<IActionResult> DeleteRoom(DeleteRoomDto deleteRoomDto)
        {
            try
            {
                var deleted = await _roomService.DeleteRoomAsync(User, deleteRoomDto);
                return deleted switch
                {
                    true => NoContent(),
                    false => BadRequest(),
                    null => NotFound()
                };
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }

        }

    }

}
