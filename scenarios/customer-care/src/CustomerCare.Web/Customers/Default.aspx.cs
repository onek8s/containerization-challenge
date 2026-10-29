using System;
using Tailspin.CustomerCare.Data.Repositories;
using Tailspin.CustomerCare.Web.Security;

namespace Tailspin.CustomerCare.Web.Customers
{
    public partial class CustomerList : CarePage
    {
        private const string QueryKey = "Customers.Query";

        protected void Page_Load(object sender, EventArgs e)
        {
            if (IsPostBack) return;

            Query.Text = Request.QueryString["q"] ?? Session[QueryKey] as string;
            if (!string.IsNullOrWhiteSpace(Query.Text)) Bind();
        }

        protected void FindButton_Click(object sender, EventArgs e)
        {
            Session[QueryKey] = Query.Text;
            Bind();
        }

        private void Bind()
        {
            Results.DataSource = new CustomerRepository().Search(Query.Text, 100);
            Results.DataBind();
        }
    }
}
