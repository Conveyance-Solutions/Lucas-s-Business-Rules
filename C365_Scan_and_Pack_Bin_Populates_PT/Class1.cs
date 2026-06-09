using P21.Extensions.BusinessRule;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace C365_Scan_and_Pack_Bin_Populates_PT_V2
//  P21 Windows and Fields used:
//  Workbench
//  Rule Type - Validator
//  Single Row Rule
//  Fields: pick_ticket_no (d_dw_scan_pack_dataentry), ufc_scan_pack_ud_deposit_cone (d_dw_scan_pack_dataentry)
//  Trigger: ufc_scan_pack_ud_deposit_cone (d_dw_scan_pack_dataentry)
{
    public class C365_Scan_and_Pack_Bin_Populates_PT_V2 : P21.Extensions.BusinessRule.Rule
    {
        public override RuleResult Execute()
        {
            RuleResult result = new RuleResult();
            try
            {
                // Retrieve the scanned bin code from custom field
                string divider = Data.Fields["ufc_scan_pack_ud_deposit_cone"].FieldValue;

                // Get the associated Pick Ticket number
                string pickTicketNumber = GetPickTicketFromBin(divider);

                // Assign it to the pick_ticket_no field
                Data.Fields["pick_ticket_no"].FieldValue = pickTicketNumber;

                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"An error occurred: {ex.Message}";
            }
            return result;
        }

        // Method to retrieve the pick ticket number from the bin code
        private string GetPickTicketFromBin(string divider)
        {
            string sql = @"
                SELECT TOP 1
                    dlb.document_no
                FROM document_line_bin dlb WITH (NOLOCK)
                INNER JOIN oe_pick_ticket oep WITH (NOLOCK)
                    ON oep.pick_ticket_no = dlb.document_no
                    AND dlb.document_type = 'PT'
                INNER JOIN oe_pick_ticket_detail oepd WITH (NOLOCK)
                    ON oep.pick_ticket_no = oepd.pick_ticket_no
                LEFT JOIN invoice_hdr ih WITH (NOLOCK)
                    ON ih.order_no = oep.order_no
                WHERE oep.scan_pack_uid IS NULL
                    AND dlb.bin_cd = @divider
                    AND oepd.unit_quantity > 0
                    AND ih.invoice_no IS NULL
                ORDER BY dlb.date_last_modified DESC
                ";

            try
            {
                using (SqlConnection con = P21SqlConnection)
                using (SqlCommand cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "@divider",
                        Value = divider
                    });

                    object result = cmd.ExecuteScalar();

                    if (result != null)
                        return result.ToString();
                    else
                        throw new Exception("No matching pick ticket found for the scanned bin.");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving Pick Ticket: {ex.Message}");
            }
        }

        public override string GetDescription()
        {
            return "Sets the Pick Ticket number in Scan and Pack based on the scanned Bin Code.";
        }

        public override string GetName()
        {
            return "C365_Scan_and_Pack_Bin_Populates_PT_V2";
        }
    }
}