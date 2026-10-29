<template>
  <div>
    <b-navbar toggleable="md" type="dark" class="navbar-lucerne">
      <b-navbar-brand to="/">{{ config.bankName || 'Lucerne Savings Bank' }}</b-navbar-brand>
      <b-navbar-toggle target="nav" />
      <b-collapse id="nav" is-nav>
        <b-navbar-nav>
          <b-nav-item to="/calculator">Loan calculator</b-nav-item>
          <b-nav-item to="/apply">Apply</b-nav-item>
          <b-nav-item to="/applications">My applications</b-nav-item>
          <b-nav-item to="/loans">My loans</b-nav-item>
        </b-navbar-nav>
        <b-navbar-nav class="ml-auto">
          <b-nav-item-dropdown right :text="me ? me.name : ''">
            <b-dropdown-item href="/account/manage/two-factor">Authenticator app</b-dropdown-item>
            <b-dropdown-form action="/account/logout" method="post"><b-button type="submit" size="sm" variant="link">Sign out</b-button></b-dropdown-form>
          </b-nav-item-dropdown>
        </b-navbar-nav>
      </b-collapse>
    </b-navbar>
    <div class="page">
      <router-view :me="me" :config="config" />
    </div>
    <footer class="text-center text-muted small mb-4">Customer service {{ config.supportPhone }}</footer>
  </div>
</template>

<script>
import api from './api'

export default {
  data: () => ({ me: null, config: {} }),
  async created () {
    this.config = await api.config()
    this.me = await api.me()
  }
}
</script>
