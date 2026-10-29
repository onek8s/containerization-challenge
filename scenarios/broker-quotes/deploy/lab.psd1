# Settings for the single-server lab (host01.corp.local). All host names point to host01.
# The lab cannot run RateCore (see README, "Lab server"), so the rating provider is the Simulator.
@{
    ZoneName          = 'corp.local'
    WebServer         = 'host01'
    AppServer         = 'host01'
    SqlServer         = 'host01'
    MqServer          = 'host01'
    SmtpServer        = 'host01'
    RatingProvider    = 'Simulator'
    SiteRoot          = 'C:\inetpub\sites'
    ServiceRoot       = 'C:\Services\Humongous'
    DataDrive         = 'C:'
    BordereauDelivery = 'Folder'
    MailMode          = 'Pickup'
}
