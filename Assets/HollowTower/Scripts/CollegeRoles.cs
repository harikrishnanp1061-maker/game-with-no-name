using System.Collections.Generic;

namespace HollowTower
{
    /// <summary>
    /// What each room of the tower is as part of the abandoned college: a display label (shown on the map HUD)
    /// and a role that the editor's dressing pass furnishes it by. Room names in LevelLayout are left alone
    /// because the older procedural game still looks rooms up by those names.
    /// </summary>
    public static class CollegeRoles
    {
        struct Entry { public string label, role; public Entry(string l, string r) { label = l; role = r; } }

        // key: "floor:room name" (floor 0 = ground)
        static readonly Dictionary<string, Entry> Table = new Dictionary<string, Entry>
        {
            // ---- floor 1: entrance, student services, dining, plant rooms
            { "0:Mail room", new Entry("Campus Post Room", "mail") },
            { "0:Security office", new Entry("Campus Security", "security") },
            { "0:Main lobby", new Entry("Entrance Hall", "lobby") },
            { "0:Information Desk", new Entry("Enquiries", "enquiries") },
            { "0:Holding Cell", new Entry("Trophy Hall", "trophy") },
            { "0:Reception office", new Entry("Admissions Office", "admin") },
            { "0:Service hallway", new Entry("Service Hallway", "passage") },
            { "0:Maintenance room", new Entry("Maintenance Room", "plant") },
            { "0:Washrooms", new Entry("Washrooms", "washroom") },
            { "0:Staff locker room", new Entry("Sports Changing Room", "lockers") },
            { "0:Cafeteria", new Entry("Dining Hall", "cafeteria") },
            { "0:Food Prep", new Entry("Kitchen Prep", "kitchen") },
            { "0:Cold Storage", new Entry("Cold Store", "coldstore") },
            { "0:Kitchen", new Entry("Kitchen", "kitchen") },
            { "0:Dishwashing", new Entry("Scullery", "scullery") },
            { "0:Boiler room", new Entry("Boiler Room", "plant") },
            { "0:Water Pump Station", new Entry("Pump Room", "plant") },
            { "0:Loading dock", new Entry("Deliveries Bay", "loading") },
            { "0:Generator room", new Entry("Generator Room", "plant") },
            { "0:Atrium", new Entry("Botanical Atrium", "botany") },
            { "0:Sculpture Gallery", new Entry("Natural History Gallery", "museum") },
            { "0:Gift shop", new Entry("Campus Bookstore", "bookstore") },
            { "0:Waiting room", new Entry("Student Services", "waiting") },
            { "0:Consultation Room", new Entry("Counselling Office", "counselling") },
            { "0:Parking access", new Entry("Parking Access", "parking") },

            // ---- floor 2: faculty of sciences and arts
            { "1:Stair lobby", new Entry("Stair Lobby", "passage") },
            { "1:Supply closet", new Entry("Supply Closet", "storage") },
            { "1:Janitor's closet", new Entry("Janitor's Closet", "janitor") },
            { "1:Classroom 2A", new Entry("Lecture Hall 2A", "lecture") },
            { "1:Classroom 2B", new Entry("Classroom 2B", "classroom") },
            { "1:Classroom 2C", new Entry("Geography Room", "geography") },
            { "1:Classroom 2D", new Entry("Lecture Hall 2D", "lecture") },
            { "1:Staff room", new Entry("Staff Common Room", "staffroom") },
            { "1:Boys' washroom", new Entry("Men's Washroom", "washroom") },
            { "1:Girls' washroom", new Entry("Women's Washroom", "washroom") },
            { "1:Nurse's office", new Entry("Infirmary", "infirmary") },
            { "1:Science lab", new Entry("Chemistry Lab", "chemlab") },
            { "1:Biology Lab", new Entry("Biology Lab", "biolab") },
            { "1:Art room", new Entry("Art Studio", "art") },
            { "1:Photo Darkroom", new Entry("Photography Darkroom", "darkroom") },
            { "1:Classroom 2E", new Entry("Physics Lab", "physlab") },
            { "1:Classroom 2F", new Entry("Seminar Room 2F", "seminar") },
            { "1:Classroom 2G", new Entry("Exam Hall", "exam") },
            { "1:Classroom 2H", new Entry("Mathematics Room", "classroom") },
            { "1:Library", new Entry("Library", "library") },
            { "1:Archives Room", new Entry("Rare Books Archive", "archive") },
            { "1:Music room", new Entry("Music Room", "music") },
            { "1:Computer lab", new Entry("Computer Lab", "computer") },
            { "1:Classroom 2J", new Entry("Engineering Workshop", "workshop") },
            { "1:Principal's office", new Entry("Dean's Office", "dean") },
            { "1:Classroom 2K", new Entry("Classroom 2K", "classroom") },
            { "1:Store room", new Entry("Store Room", "storage") },
            { "1:Locker room", new Entry("Locker Room", "lockers") },

            // ---- floor 3: residence hall
            { "2:Linen room", new Entry("Linen Room", "linen") },
            { "2:Hotel wing", new Entry("Residents' Common Room", "common") },
            { "2:Penthouse suite", new Entry("Warden's Flat", "warden") },
            { "2:Washrooms", new Entry("Shower Block", "showers") },
            { "2:Laundry", new Entry("Laundry", "laundry") },
            { "2:Linen Storage", new Entry("Linen Storage", "linen") },
            { "2:Room 325", new Entry("Greenhouse", "greenhouse") },
            { "2:Room 326", new Entry("Herbarium", "herbarium") },
            { "2:Hotel lounge", new Entry("Student Lounge", "lounge") },
            { "2:Cigar Room", new Entry("Reading Room", "reading") },
            { "2:Room 318", new Entry("Study Hall", "study") },
            { "2:Room 311", new Entry("Music Practice Room", "practice") },
            { "2:Housekeeping", new Entry("Housekeeping", "housekeeping") },
            { "2:Ice & vending", new Entry("Vending Corner", "vending") },
            { "2:Service closet", new Entry("Service Closet", "janitor") },
            { "2:Stair lobby", new Entry("Stair Lobby", "passage") },
            { "2:Staff washroom", new Entry("Staff Washroom", "washroom") },

            // ---- floor 4: administration and faculty offices
            { "3:Stair lobby", new Entry("Stair Lobby", "passage") },
            { "3:Supply room", new Entry("Supply Room", "storage") },
            { "3:Janitor's closet", new Entry("Janitor's Closet", "janitor") },
            { "3:Office 401", new Entry("Professor's Office 401", "professor") },
            { "3:Office 402", new Entry("Professor's Office 402", "professor") },
            { "3:Office 403", new Entry("Professor's Office 403", "professor") },
            { "3:Security Control", new Entry("CCTV Control", "cctv") },
            { "3:Meeting room A", new Entry("Committee Room A", "meeting") },
            { "3:Meeting room B", new Entry("Committee Room B", "meeting") },
            { "3:Executive office", new Entry("Vice-Chancellor's Office", "dean") },
            { "3:Records room", new Entry("Records Office", "records") },
            { "3:Men's washroom", new Entry("Men's Washroom", "washroom") },
            { "3:Women's washroom", new Entry("Women's Washroom", "washroom") },
            { "3:Copy room", new Entry("Print Room", "copy") },
            { "3:Cubicle farm", new Entry("Faculty Workroom", "workroom") },
            { "3:Finance Office", new Entry("Bursar's Office", "bursar") },
            { "3:Break room", new Entry("Staff Tea Room", "staffroom") },
            { "3:Archive", new Entry("Archive", "archive") },
            { "3:Conference hall", new Entry("Senate Chamber", "senate") },
            { "3:Briefing Room B", new Entry("Seminar Room B", "seminar") },
            { "3:Office 4A", new Entry("Faculty Office 4A", "professor") },
            { "3:Office 4B", new Entry("Faculty Office 4B", "professor") },
            { "3:Office 4C", new Entry("Faculty Office 4C", "professor") },
            { "3:Server room", new Entry("Server Room", "server") },
            { "3:IT office", new Entry("IT Helpdesk", "it") },
            { "3:Storage", new Entry("Storage", "storage") },
            { "3:Open-plan office 2", new Entry("Faculty Workroom 2", "workroom") },
            { "3:Legal Records", new Entry("Student Records", "records") },
            { "3:Kitchenette", new Entry("Kitchenette", "staffroom") },
            { "3:Manager's office", new Entry("Registrar's Office", "admin") },
            { "3:Director's office", new Entry("Chancellor's Office", "dean") },
            { "3:Washroom", new Entry("Washroom", "washroom") },
            { "3:Storage 2", new Entry("Storage 2", "storage") },
        };

        public static void Apply(FloorPlan p)
        {
            foreach (var room in p.rooms)
            {
                Entry e;
                if (Table.TryGetValue(p.floor + ":" + room.name, out e)) { room.label = e.label; room.role = e.role; }
                else if (p.floor == 2 && room.type == RoomType.HotelRoom) { room.label = room.name.Replace("Room", "Dorm"); room.role = "dorm"; }
                else room.role = room.type == RoomType.Corridor ? "corridor" : "storage";
            }
        }
    }
}
