using P21.Extensions.BusinessRule;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace C365_OE_Line_FET
{
    public class C365_OE_Line_FET : P21.Extensions.BusinessRule.Rule
    {
        // Main execution method for the business rule
        public override RuleResult Execute()
        {
            RuleResult result = new RuleResult();

            try
            {
                // Retrieve the item ID from the order line
                string itemId = Data.Fields["oe_order_item_id"].FieldValue;

                // Fetch the FET amount for the given item
                decimal fetAmount = GetItemFET(itemId);

                // If the FET amount is null or blank, do not run
                if (fetAmount == 0.00M)
                {
                    result.Success = true;
                    return result;
                }

                // Update the custom field with the FET amount
                Data.Fields["ufc_oe_line_ud_fet_line"].FieldValue = fetAmount.ToString();

                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"An error occurred: {ex.Message}";
            }

            return result;
        }

        // Method to fetch the FET amount for a given item ID
        private decimal GetItemFET(string itemId)
        {
            DataTable dt = new DataTable();

            // SQL query to retrieve FET amount from inventory tables
            string sql = @"
                SELECT TOP 1 imud.fet_amount
                FROM inv_mast im
                LEFT JOIN inv_mast_ud imud WITH(NOLOCK)
                ON im.inv_mast_uid = imud.inv_mast_uid
                WHERE im.item_id = @item_id
            ";

            try
            {
                // Establish database connection and execute query
                using (SqlConnection con = P21SqlConnection)
                using (SqlCommand cmd = new SqlCommand(sql, con))
                {
                    // Add parameter to prevent SQL injection
                    cmd.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "@item_id",
                        Value = itemId
                    });

                    // Execute query and load results into DataTable
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        dt.Load(reader);
                    }
                }

                // Check if any rows were returned
                if (dt.Rows.Count > 0)
                {
                    var fetAmountValue = dt.Rows[0]["fet_amount"];

                    // Return the FET amount or 0 if the value is null
                    return fetAmountValue == DBNull.Value ? 0.00M : Convert.ToDecimal(fetAmountValue);
                }
            }
            catch (Exception ex)
            {
                // Handle exceptions during database query execution
                throw new Exception($"Error retrieving FET amount: {ex.Message}");
            }

            // Return 0 if no data was found
            return 0.00M;
        }

        // Description of the rule
        public override string GetDescription()
        {
            return "Sets the FET (Federal Excise Tax) amount on the OE line based on item data.";
        }

        // Name of the rule
        public override string GetName()
        {
            return "C365_OE_Line_FET";
        }
    }
}

