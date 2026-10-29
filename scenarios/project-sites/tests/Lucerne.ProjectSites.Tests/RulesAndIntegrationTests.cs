using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using Lucerne.ProjectSites.Contracts.DocumentControl;
using Lucerne.ProjectSites.Contracts.Erp;
using Lucerne.ProjectSites.Core.Services;
using Lucerne.ProjectSites.Documents;
using Lucerne.ProjectSites.Domain;
using Lucerne.ProjectSites.Integration;
using Lucerne.ProjectSites.Integration.DocumentControl;
using Lucerne.ProjectSites.Integration.Erp;
using Lucerne.ProjectSites.Integration.Hr;
using Lucerne.ProjectSites.Integration.Mapping;
using Lucerne.ProjectSites.LabStubs;
using Lucerne.ProjectSites.LabStubs.InMemory;
using Lucerne.ProjectSites.LabStubs.Seed;
using Lucerne.ProjectSites.LabStubs.Simulators;
using Lucerne.ProjectSites.Sync;
using OfficeOpenXml;
using Xunit;

namespace Lucerne.ProjectSites.Tests
{
    public class DocumentRulesTests
    {
        [Theory]
        [InlineData("setup.exe")]
        [InlineData("deploy.PS1")]
        public void Executables_are_blocked(string fileName)
        {
            Assert.NotNull(DocumentRules.ValidateAdd(new DocumentState { FileName = fileName }, "STR"));
        }

        [Fact]
        public void Discipline_must_match_library()
        {
            Assert.NotNull(DocumentRules.ValidateAdd(new DocumentState { FileName = "a.pdf", Discipline = "MEP" }, "STR"));
            Assert.Null(DocumentRules.ValidateAdd(new DocumentState { FileName = "a.pdf", Discipline = "STR", DocumentType = "DWG", Revision = "P01" }, "STR"));
        }

        [Fact]
        public void Issued_documents_need_contractual_revision()
        {
            var before = new DocumentState { Revision = "P03", Status = DeliverableStatus.ForReview };
            Assert.NotNull(DocumentRules.ValidateUpdate(before, new DocumentState { Revision = "P03", Status = DeliverableStatus.Issued }));
            Assert.Null(DocumentRules.ValidateUpdate(before, new DocumentState { Revision = "C01", Status = DeliverableStatus.Issued }));
        }

        [Fact]
        public void Allocated_numbers_cannot_be_edited()
        {
            var before = new DocumentState { DocumentNumber = "P19041-LUC-STR-DWG-0012", Revision = "P01" };
            var after = new DocumentState { DocumentNumber = "P19041-LUC-STR-DWG-0013", Revision = "P01" };
            Assert.NotNull(DocumentRules.ValidateUpdate(before, after));
        }

        [Fact]
        public void Records_and_issued_documents_cannot_be_deleted()
        {
            Assert.NotNull(DocumentRules.ValidateDelete(new DocumentState { IsRecord = true }));
            Assert.NotNull(DocumentRules.ValidateDelete(new DocumentState { Status = DeliverableStatus.Issued }));
            Assert.Null(DocumentRules.ValidateDelete(new DocumentState { Status = DeliverableStatus.WorkInProgress }));
        }

        [Fact]
        public void Allocator_counts_per_discipline_and_type()
        {
            var farm = new InMemoryFarm();
            farm.CreateSite("http://projects.corp.local/sites/p19041", "P19041", Enumerable.Empty<Lucerne.ProjectSites.Core.ListDefinition>());
            using (var site = farm.Open("http://projects.corp.local/sites/p19041"))
            {
                var allocator = new DocumentNumberAllocator(site);
                Assert.Equal("P19041-LUC-STR-DWG-0001", allocator.Next("P19041", "STR", "DWG").ToString());
                Assert.Equal("P19041-LUC-STR-DWG-0002", allocator.Next("P19041", "STR", "DWG").ToString());
                Assert.Equal("P19041-LUC-MEP-DWG-0001", allocator.Next("P19041", "MEP", "DWG").ToString());

                DocumentNumber imported;
                Assert.True(DocumentNumber.TryParse("P19041-LUC-STR-DWG-0040", out imported));
                allocator.Observe(imported);
                Assert.Equal("P19041-LUC-STR-DWG-0041", allocator.Next("P19041", "STR", "DWG").ToString());
            }
        }
    }

    public class IntegrationProfileTests
    {
        [Fact]
        public void Configuration_is_valid()
        {
            new AutoMapper.MapperConfiguration(c => c.AddProfile<IntegrationProfile>()).AssertConfigurationIsValid();
        }

        [Theory]
        [InlineData(10, ProjectStage.Bid)]
        [InlineData(20, ProjectStage.Active)]
        [InlineData(50, ProjectStage.Closed)]
        [InlineData(90, ProjectStage.Lost)]
        public void Erp_status_maps_to_stage(int code, ProjectStage stage)
        {
            Assert.Equal(stage, IntegrationProfile.StageFromStatus(code));
        }

        [Fact]
        public void Erp_project_maps_office_and_disciplines()
        {
            var project = IntegrationProfile.CreateMapper().Map<Project>(new ErpProject
            {
                ProjectNo = "P19041", Description = "Seebrücke", ProfitCentre = "zrh-210", StatusCode = 20, DisciplineCodes = "str; mep,XYZ,STR"
            });

            Assert.Equal("ZRH", project.OfficeCode);
            Assert.Equal(new[] { "STR", "MEP" }, project.Disciplines);
            Assert.Equal(ProjectStage.Active, project.Stage);
        }

        [Theory]
        [InlineData("2110", "STR")]
        [InlineData("3410", "MEP")]
        [InlineData("1900", null)]
        public void Cost_centre_second_digit_is_discipline(string costCentre, string discipline)
        {
            Assert.Equal(discipline, IntegrationProfile.DisciplineFromCostCentre(costCentre));
        }
    }

    public class SyncStateTests
    {
        [Fact]
        public void Json_store_round_trips_watermarks_and_runs()
        {
            var path = Path.Combine(Path.GetTempPath(), "lucerne-tests-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var mark = new DateTime(2019, 5, 6, 7, 8, 9, DateTimeKind.Utc);
                var store = new JsonFileSyncStateStore(path);
                store.SetWatermark("erp.projects", mark);
                store.RecordRun(new SyncRun { Job = "projects", Succeeded = true, Created = 3 });

                var reopened = new JsonFileSyncStateStore(path);
                Assert.Equal(mark, reopened.GetWatermark("erp.projects").Value.ToUniversalTime());
                Assert.Null(reopened.GetWatermark("hr.staff"));
            }
            finally
            {
                File.Delete(path);
            }
        }
    }

    public class DocumentGenerationTests
    {
        [Fact]
        public void Transmittal_sheet_is_a_valid_word_document()
        {
            var bytes = TransmittalSheet.Create("P18007", "Hallenbad Ost", "P18007-TRN-0001", "Bauherr", "Lucerne Engineering", new DateTime(2019, 6, 3),
                new[] { new TransmittalLine { DocumentNumber = "P18007-LUC-STR-DWG-0001", Title = "Foundation plan", Revision = "C01" } });

            using (var doc = WordprocessingDocument.Open(new MemoryStream(bytes), false))
            {
                Assert.Contains("P18007-LUC-STR-DWG-0001", doc.MainDocumentPart.Document.InnerText);
            }
        }

        [Fact]
        public void Hours_workbook_has_one_row_per_employee()
        {
            var week = IsoWeek.FromDate(new DateTime(2019, 6, 3));
            var bytes = Workbooks.HoursByWeek(new[]
            {
                new HoursRow { ProjectNumber = "P18007", EmployeeId = "10001", EmployeeName = "A", Office = "LUZ", Discipline = "STR", Week = week, Hours = 32 },
                new HoursRow { ProjectNumber = "P18007", EmployeeId = "10002", EmployeeName = "B", Office = "ZRH", Discipline = "MEP", Week = week, Hours = 8 }
            });

            using (var package = new ExcelPackage(new MemoryStream(bytes)))
            {
                Assert.NotEmpty(package.Workbook.Worksheets);
                var sheet = package.Workbook.Worksheets.First();
                Assert.True(sheet.Dimension.End.Row >= 3);
            }
        }
    }

    public class SimulatorClientTests
    {
        private static int FreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        [Fact]
        public async Task Clients_talk_to_simulators()
        {
            RetryPolicies.DelayProvider = _ => TimeSpan.FromMilliseconds(10);
            var seed = new SeedData(new DateTime(2019, 6, 14));
            await using (var host = new SimulatorHost(seed, FreePort()))
            {
                await host.StartAsync();

                var erp = new ErpProjectClient(new ErpSettings { Endpoint = host.BaseUrl + "/ProjectAccounting/ProjectService.svc", PageSize = 5 });
                Assert.Equal(seed.Projects.Count, erp.GetChangedProjects(new DateTime(2000, 1, 1)).Count());
                Assert.Equal("P19041", erp.GetProject("P19041").ProjectNo);

                var hr = new HrDirectoryClient(new HrSettings { BaseUrl = host.BaseUrl + "/hrapi", ApiKey = SimulatorHost.HrApiKey, PageSize = 20 });
                Assert.Equal(seed.Employees.Count, hr.GetEmployeesChangedSince(null).Count());

                var badKey = new HrDirectoryClient(new HrSettings { BaseUrl = host.BaseUrl + "/hrapi", ApiKey = "wrong" });
                Assert.ThrowsAny<Exception>(() => badKey.GetEmployeesChangedSince(null).ToList());

                var dc = new DocumentControlClient(new DocumentControlSettings { BaseUrl = host.BaseUrl + "/doccontrol", ClientId = SimulatorHost.DocControlClientId, ClientSecret = SimulatorHost.DocControlSecret });
                var receipt = dc.Submit(new DocumentSubmission
                {
                    ProjectNumber = "P18007", DocumentNumber = "P18007-LUC-STR-DWG-0001", Title = "Foundation plan", Revision = "C01",
                    Discipline = "STR", DocumentType = "DWG", FileName = "P18007-LUC-STR-DWG-0001.pdf"
                }, new byte[] { 1, 2, 3 });
                Assert.False(string.IsNullOrEmpty(receipt.SubmissionId));
                Assert.NotNull(dc.GetStatus(receipt.SubmissionId));
            }
        }
    }

    public class LabRunTests
    {
        [Fact]
        public async Task End_to_end_run_passes_all_checks()
        {
            var output = Path.Combine(Path.GetTempPath(), "lucerne-tests-lab-" + Guid.NewGuid().ToString("N"));
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            try
            {
                var result = await new LabRunner(new DateTime(2019, 6, 14), output, port).RunAsync();
                Assert.Equal(0, result);
                Assert.True(File.Exists(Path.Combine(output, "portfolio.xlsx")));
            }
            finally
            {
                if (Directory.Exists(output))
                {
                    Directory.Delete(output, true);
                }
            }
        }
    }
}
