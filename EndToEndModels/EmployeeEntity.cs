using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class EmployeeEntity
{
    [JsonProperty(PropertyName = "id")]
    private string mId = null;
    public string Id 
    {
        get 
        {
            if (mId == null)
            {
                mId = Guid.NewGuid().ToString();
            }

            return mId;
        }

        set
        {
            mId = value;
        }
    }

    [JsonProperty(PropertyName = "firstname")]
    public string FirstName { get; set; }

    [JsonProperty(PropertyName = "lastname")]
    public string LastName { get; set; }

    [JsonProperty(PropertyName = "dateofbirth")]
    public DateTime DateOfBirth { get; set; }
}
