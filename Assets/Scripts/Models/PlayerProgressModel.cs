using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Supabase.Postgrest.Models;
using Supabase.Postgrest.Attributes;

namespace RoguelikeSocial.Assets.Scripts.Models
{
    [Table("player_progress")]
    public class PlayerProgressModel : BaseModel
    {
        [PrimaryKey("id", false)]
        public string Id { get; set; }

        [Column("username")]
        public string Username { get; set; }

        [Column("level")]
        public int Level { get; set; }

        [Column("last_room")]
        public int LastRoom { get; set; }

        [Column("total_score")]
        public int TotalScore { get; set; }

        [Column("payload")]
        public object Payload { get; set; }
    }
}