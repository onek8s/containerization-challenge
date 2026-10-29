import React from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter, Route, Routes } from "react-router-dom";
import { Layout } from "./components/Layout";
import { Dashboard } from "./pages/Dashboard";
import { SitePage } from "./pages/SitePage";
import { SubjectPage } from "./pages/SubjectPage";
import { CasebookPage } from "./pages/CasebookPage";
import { QueriesPage } from "./pages/QueriesPage";
import { CodingPage } from "./pages/CodingPage";
import "./styles.css";

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <BrowserRouter>
      <Routes>
        <Route element={<Layout />}>
          <Route index element={<Dashboard />} />
          <Route path="studies/:studyId" element={<Dashboard />} />
          <Route path="sites/:siteId" element={<SitePage />} />
          <Route path="subjects/:subjectId" element={<SubjectPage />} />
          <Route path="subjects/:subjectId/casebook" element={<CasebookPage />} />
          <Route path="studies/:studyId/queries" element={<QueriesPage />} />
          <Route path="coding" element={<CodingPage />} />
        </Route>
      </Routes>
    </BrowserRouter>
  </React.StrictMode>
);
