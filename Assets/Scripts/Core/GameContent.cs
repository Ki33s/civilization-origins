using System;
using System.Collections.Generic;

namespace Origins
{
    [Serializable] public sealed class ChoiceRecord
    {
        public string Id, Name, Description, Requires, Unit;
        public int Cost;
        public string[] RequiresAll;
        public string SiteRule;
        public bool Wonder;
        public int MinPopulation,BonusFood,BonusProduction,BonusScience,BonusCulture;
    }
    [Serializable] public sealed class ContentRecords
    {
        public string Version;
        public ChoiceRecord[] Technologies, Cultures, Projects;
    }
    public sealed class GameContent
    {
        public static GameContent Current;
        public readonly string Version;
        public readonly Choice[] Technologies, Cultures, Projects;
        public GameContent(ContentRecords records)
        {
            if(records==null || string.IsNullOrEmpty(records.Version)) throw new ArgumentException("Missing content version");
            Version=records.Version; Technologies=Convert(records.Technologies); Cultures=Convert(records.Cultures); Projects=Convert(records.Projects);
            var openings=new HashSet<string>(); foreach(var c in Technologies) openings.Add(c.Id); foreach(var c in Cultures) if(!openings.Add(c.Id)) throw new ArgumentException("Duplicate opening "+c.Id);
            foreach(var list in new[]{Technologies,Cultures,Projects}) foreach(var c in list)
            {
                if(!string.IsNullOrEmpty(c.Requires) && !openings.Contains(c.Requires)) throw new ArgumentException("Unknown prerequisite "+c.Requires);
                foreach(var id in c.RequiresAll) if(!openings.Contains(id)) throw new ArgumentException("Unknown prerequisite "+id);
            }
        }
        static Choice[] Convert(ChoiceRecord[] records)
        {
            if(records==null || records.Length==0) throw new ArgumentException("Empty content catalog");
            var result=new Choice[records.Length]; var ids=new HashSet<string>();
            for(int i=0;i<records.Length;i++)
            {
                var c=records[i]; if(string.IsNullOrEmpty(c.Id) || string.IsNullOrEmpty(c.Name) || c.Cost<=0 || !ids.Add(c.Id)) throw new ArgumentException("Invalid content entry");
                UnitKind? unit=null;
                if(!string.IsNullOrEmpty(c.Unit)) { UnitKind kind; if(!Enum.TryParse(c.Unit,out kind)) throw new ArgumentException("Unknown unit "+c.Unit); unit=kind; }
                result[i]=new Choice(c.Id,c.Name,c.Description,c.Cost,string.IsNullOrEmpty(c.Requires) ? null : c.Requires,unit);
                result[i].RequiresAll=c.RequiresAll??new string[0]; result[i].SiteRule=c.SiteRule; result[i].Wonder=c.Wonder; result[i].MinPopulation=c.MinPopulation;
                result[i].BonusFood=c.BonusFood; result[i].BonusProduction=c.BonusProduction; result[i].BonusScience=c.BonusScience; result[i].BonusCulture=c.BonusCulture;
            }
            return result;
        }
    }
}
