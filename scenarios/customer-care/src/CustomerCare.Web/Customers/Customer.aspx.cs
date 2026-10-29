using System;
using System.Web;
using Tailspin.CustomerCare.Data.Repositories;
using Tailspin.CustomerCare.Domain;
using Tailspin.CustomerCare.Web.Security;

namespace Tailspin.CustomerCare.Web.Customers
{
    public partial class CustomerDetail : CarePage
    {
        private readonly CustomerRepository _customers = new CustomerRepository();

        private int CustomerId
        {
            get { return (int)ViewState["CustomerId"]; }
            set { ViewState["CustomerId"] = value; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (IsPostBack) return;

            var id = QueryInt("id");
            var customer = id.HasValue ? _customers.Get(id.Value) : null;
            if (customer == null) throw new HttpException(404, "Customer not found.");

            CustomerId = customer.Id;
            Show(customer);
        }

        protected void SaveButton_Click(object sender, EventArgs e)
        {
            if (!IsValid) return;

            var c = _customers.Get(CustomerId);
            c.Name = CustomerName.Text.Trim();
            c.Email = Empty(Email.Text);
            c.Phone = Empty(Phone.Text);
            c.Street = Empty(Street.Text);
            c.PostalCode = Empty(PostalCode.Text);
            c.City = Empty(City.Text);
            c.IsPriorityCustomer = PriorityCustomer.Checked;

            if (Try(() => _customers.Update(c))) ShowMessage("Customer saved.");
            Show(_customers.Get(CustomerId));
        }

        private void Show(Customer c)
        {
            Title = c.Name;
            CustomerTitle.Text = c.Name;
            NewCaseLink.NavigateUrl = "~/cases/new?customer=" + c.Id;
            AccountNumber.Text = c.AccountNumber;
            Segment.Text = c.Segment;
            CustomerName.Text = c.Name;
            Email.Text = c.Email;
            Phone.Text = c.Phone;
            Street.Text = c.Street;
            PostalCode.Text = c.PostalCode;
            City.Text = c.City;
            PriorityCustomer.Checked = c.IsPriorityCustomer;

            CaseHistory.DataSource = new CaseRepository().ListForCustomer(c.Id);
            CaseHistory.DataBind();
        }

        private static string Empty(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
