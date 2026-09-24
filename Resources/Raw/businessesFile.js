class Business {
    constructor({ name, safeword, alternativeExit, imgSrc }) {
        this.name = name;
        this.safeword = safeword;
        this.alternativeExit = alternativeExit;
        this.imgSrc = imgSrc;
        this.openedHours = {};
        this.closedHours = {};
        this.isClosed = false;
        this.markerRef = null;
    }

    setDist(dist) {
        this.dist = dist;
    }

    setOpeningHours(mon, tue, wed, thu, fri, sat, sun) {
        this.openedHours["Monday"] = mon;
        this.openedHours["Tuesday"] = tue;
        this.openedHours["Wednesday"] = wed;
        this.openedHours["Thursday"] = thu;
        this.openedHours["Friday"] = fri;
        this.openedHours["Saturday"] = sat;
        this.openedHours["Sunday"] = sun;
    }

    setClosingHours(mon, tue, wed, thu, fri, sat, sun) {
        this.closedHours["Monday"] = mon;
        this.closedHours["Tuesday"] = tue;
        this.closedHours["Wednesday"] = wed;
        this.closedHours["Thursday"] = thu;
        this.closedHours["Friday"] = fri;
        this.closedHours["Saturday"] = sat;
        this.closedHours["Sunday"] = sun;
    }

    displayInfo() {
        return this.name;
    }
}

const PRECOOKED_BUSINESSES = [
    {
        name: 'Victoria Cannabis Company',
        safeword: 'bonkers',
        alternativeExit: 'Yes',
        imgSrc: 'https://pub-0c6f5b8a360c417e9ac2670079e7c2c9.r2.dev/vcc_star.png',
        lng: -123.38619508440203,
        lat: 48.43005130786145,
        open: ['09:00', '09:00', '09:00', '09:00', '09:00', '09:00', '09:00'],
        close: ['23:00', '23:00', '23:00', '23:00', '23:00', '23:00', '23:00']
    },
    {
        name: 'Big Wheel Burger',
        safeword: 'Duck Burger',
        alternativeExit: 'Yes',
        imgSrc: 'https://pub-0c6f5b8a360c417e9ac2670079e7c2c9.r2.dev/bwb_star.png',
        lng: -123.38085452996481,
        lat: 48.432426309628234,
        open: ['11:00', '11:00', '11:00', '11:00', '11:00', '11:00', '11:00'],
        close: ['21:00', '21:00', '21:00', '21:00', '21:00', '21:00', '21:00']
    },
    {
        name: 'Baan Thai Blanshard',
        safeword: 'Thai Papa',
        alternativeExit: 'Yes',
        imgSrc: 'https://pub-0c6f5b8a360c417e9ac2670079e7c2c9.r2.dev/BTWstarOutline.png',
        lng: -123.36213254907534,
        lat: 48.42484591493667,
        open: ['11:00', '11:00', '11:00', '11:00', '11:00', '11:00', '11:00'],
        close: ['23:00', '23:00', '23:00', '23:00', '23:00', '23:00', '23:00']
    },
    {
        name: 'Baan Thai Oak Bay',
        safeword: 'Thai Papa',
        alternativeExit: 'Yes',
        imgSrc: 'https://pub-0c6f5b8a360c417e9ac2670079e7c2c9.r2.dev/BTWstarOutline.png',
        lng: -123.31852740237463,
        lat: 48.4358049394821,
        open: ['11:00', '11:00', '11:00', '11:00', '11:00', '11:00', '11:00'],
        close: ['23:00', '23:00', '23:00', '23:00', '23:00', '23:00', '23:00']
    },
    {
        name: 'Baan Thai Langford',
        safeword: 'Thai Papa',
        alternativeExit: 'Yes',
        imgSrc: 'https://pub-0c6f5b8a360c417e9ac2670079e7c2c9.r2.dev/BTWstarOutline.png',
        lng: -123.51113145480343,
        lat: 48.438993766245495,
        open: ['11:00', '11:00', '11:00', '11:00', '11:00', '11:00', '11:00'],
        close: ['23:00', '23:00', '23:00', '23:00', '23:00', '23:00', '23:00']
    },
    {
        name: 'Baan Thai Broadmead',
        safeword: 'Thai Papa',
        alternativeExit: 'Yes',
        imgSrc: 'https://pub-0c6f5b8a360c417e9ac2670079e7c2c9.r2.dev/BTWstarOutline.png',
        lng: -123.3779222349418,
        lat: 48.499315952806576,
        open: ['11:00', '11:00', '11:00', '11:00', '11:00', '11:00', '11:00'],
        close: ['23:00', '23:00', '23:00', '23:00', '23:00', '23:00', '23:00']
    },
    {
        name: 'Selkirk Cafe',
        safeword: 'Milkless Coffee',
        alternativeExit: 'No',
        imgSrc: 'https://pub-0c6f5b8a360c417e9ac2670079e7c2c9.r2.dev/selkirk_star.png',
        lng: -123.37857177606156,
        lat: 48.44044497495145,
        open: ['7:30', '7:30', '00:00', '7:30', '7:30', '9:00', '00:00'],
        close: ['16:00', '16:00', '00:00', '16:00', '16:00', '15:00', '00:00']
    },
    {
        name: 'Superflux Cabana',
        safeword: 'Super Foggy',
        alternativeExit: 'No',
        imgSrc: 'https://pub-0c6f5b8a360c417e9ac2670079e7c2c9.r2.dev/superflux_star.png',
        lng: -123.3618611551704,
        lat: 48.423767278894374,
        open: ['00:00', '16:00', '16:00', '16:00', '12:00', '12:00', '12:00'],
        close: ['00:00', '22:00', '22:00', '22:00', '23:00', '23:00', '21:00']
    },
    {
        name: 'Birdman',
        safeword: 'chicken feet',
        alternativeExit: 'Yes',
        imgSrc: 'https://pub-0c6f5b8a360c417e9ac2670079e7c2c9.r2.dev/birdmanStar.png',
        lng: -123.36928685092374,
        lat: 48.42773841024087,
        open: ['00:00', '16:00', '16:00', '16:00', '12:00', '12:00', '12:00'],
        close: ['00:00', '22:00', '22:00', '22:00', '23:00', '23:00', '21:00']
    },
    {
        name: 'Swans Pub',
        safeword: 'bird beer',
        alternativeExit: 'Yes',
        imgSrc: 'https://pub-0c6f5b8a360c417e9ac2670079e7c2c9.r2.dev/swansStar.png',
        lng: -123.36933392915849,
        lat: 48.42871730938605,
        open: ['00:00', '16:00', '16:00', '16:00', '12:00', '12:00', '12:00'],
        close: ['00:00', '22:00', '22:00', '22:00', '23:00', '23:00', '21:00']
    },
    {
        name: 'EVA Schnitzelhaus',
        safeword: 'Schnitzel beer',
        alternativeExit: 'No',
        imgSrc: 'https://pub-0c6f5b8a360c417e9ac2670079e7c2c9.r2.dev/EvaStar.png',
        lng: -123.36915636749096,
        lat: 48.429377554642954,
        open: ['17:00', '17:00', '17:00', '17:00', '17:00', '10:00', '10:00'],
        close: ['21:00', '21:00', '21:00', '21:00', '21:00', '21:00', '21:00']
    },
    {
        name: 'Darcy\'s Pub',
        safeword: 'boat water',
        alternativeExit: 'No',
        imgSrc: 'https://pub-0c6f5b8a360c417e9ac2670079e7c2c9.r2.dev/DarcysStar.png',
        lng: -123.36986404935028,
        lat: 48.425841144765045,
        open: ['11:00', '11:00', '11:00', '11:00', '11:00', '11:00', '11:00'],
        close: ['00:00', '00:00', '00:00', '00:00', '00:00', '00:00', '00:00']
    },
    {
        name: 'Boomtown',
        safeword: 'boom boom',
        alternativeExit: 'No',
        imgSrc: 'https://pub-0c6f5b8a360c417e9ac2670079e7c2c9.r2.dev/BoomtownStar.png',
        lng: -123.35756799167952,
        lat: 48.426067580974205,
        open: ['11:30', '11:30', '11:30', '11:30', '11:30', '11:30', '11:30'],
        close: ['22:00', '22:00', '22:00', '22:00', '22:00', '22:00', '22:00']
    },
    {
        name: 'Cafe Malabar',
        safeword: 'cumin bar',
        alternativeExit: 'Yes',
        imgSrc: 'https://pub-0c6f5b8a360c417e9ac2670079e7c2c9.r2.dev/CafeMalabar_star.png',
        lng: - 123.37101763558191,
        lat: 48.42941082139318,
        open: ['00:00', '00:00', '12:00', '12:00', '12:00', '12:00', '12:00'],
        close: ['00:00', '00:00', '20:30', '20:30', '20:30', '20:30', '20:30']
    },
    {
        name: 'Syriana Restaurant',
        safeword: 'melted veggies',
        alternativeExit: 'Yes',
        imgSrc: 'https://pub-0c6f5b8a360c417e9ac2670079e7c2c9.r2.dev/Syrianna_star.png',
        lng: - 123.41620112217493,
        lat: 48.43068903100649,
        open: ['09:00', '09:00', '09:00', '09:00', '09:00', '09:00', '09:00'],
        close: ['21:00', '21:00', '21:00', '21:00', '21:00', '21:00', '21:00']
    },
    {
        name: 'Thrive and Shine',
        safeword: 'Shiny muffin',
        alternativeExit: 'No',
        imgSrc: 'https://pub-0c6f5b8a360c417e9ac2670079e7c2c9.r2.dev/Thrive_star.png',
        lng: - 123.42107805214896,
        lat: 48.4377746698216,
        open: ['07:00', '07:00', '07:00', '07:00', '07:00', '08:00', '08:00'],
        close: ['15:00', '15:00', '15:00', '15:00', '15:00', '15:00', '15:00']
    },
    {
        name: 'Herald Street Brew Works',
        safeword: 'Tomato malt',
        alternativeExit: 'Yes',
        imgSrc: 'https://pub-0c6f5b8a360c417e9ac2670079e7c2c9.r2.dev/Herald_star.png',
        lng: - 123.3693958070206,
        lat: 48.4304357735693,
        open: ['12:00', '12:00', '12:00', '12:00', '12:00', '12:00', '12:00'],
        close: ['22:00', '22:00', '22:00', '22:00', '23:00', '23:00', '21:00']
    },
    {
        name: '10 Acres Common',
        safeword: '23 Acres',
        alternativeExit: 'Yes',
        imgSrc: 'https://pub-0c6f5b8a360c417e9ac2670079e7c2c9.r2.dev/10Acres_star.png',
        lng: - 123.36774227561662,
        lat: 48.422923399800304,
        open: ['11:30', '11:30', '11:30', '11:30', '11:30', '11:30', '11:30'],
        close: ['00:00', '00:00', '00:00', '00:00', '01:00', '01:00', '23:00']
    },
    {
        name: '10 Acres Bistro',
        safeword: '23 Acres',
        alternativeExit: 'Yes',
        imgSrc: 'https://pub-0c6f5b8a360c417e9ac2670079e7c2c9.r2.dev/10Acres_star.png',
        lng: - 123.3675759786685,
        lat: 48.42340399380689,
        open: ['11:00', '11:00', '11:00', '11:00', '11:00', '10:00', '10:00'],
        close: ['23:00', '23:00', '23:00', '23:00', '00:00', '00:00', '23:00']
    },
    {
        name: 'Spoons Diner',
        safeword: 'Spoonful Sugar',
        alternativeExit: 'No',
        imgSrc: 'https://pub-0c6f5b8a360c417e9ac2670079e7c2c9.r2.dev/Spoons_star.png',
        lng: - 123.3685866355822,
        lat: 48.44211063808607,
        open: ['07:00', '07:00', '07:00', '07:00', '07:00', '07:00', '07:00'],
        close: ['15:00', '15:00', '15:00', '15:00', '15:00', '15:00', '15:00']
    },
    {
        name: 'Spinnakers Gastro Brewpub',
        safeword: 'Beer Spin',
        alternativeExit: 'Yes',
        imgSrc: 'https://pub-0c6f5b8a360c417e9ac2670079e7c2c9.r2.dev/Spinnakers_star.png',
        lng: - 123.38495459154198,
        lat: 48.42907987390003,
        open: ['09:00', '09:00', '09:00', '09:00', '09:00', '09:00', '09:00'],
        close: ['23:00', '23:00', '23:00', '23:00', '23:00', '23:00', '23:00']
    },
    {
        name: 'Vancouver Art Gallery',
        safeword: '',
        alternativeExit: '',
        imgSrc: 'https://pub-0c6f5b8a360c417e9ac2670079e7c2c9.r2.dev/vag_star.png',
        lng: -123.12060576585512,
        lat: 49.28302361456653,
        open: ['10:00', '10:00', '10:00', '10:00', '10:00', '10:00', '10:00'],
        close: ['17:00', '17:00', '17:00', '17:00', '20:00', '17:00', '17:00']
    },
    {
        name: 'Hilton Hotel',
        safeword: '',
        alternativeExit: '',
        imgSrc: 'https://pub-0c6f5b8a360c417e9ac2670079e7c2c9.r2.dev/hilton_marker1.png',
        lng: -123.11700532742789,
        lat: 49.27995983925699,
        open: ['00:00', '00:00', '00:00', '00:00', '00:00', '00:00', '00:00'],
        close: ['23:59', '23:59', '23:59', '23:59', '23:59', '23:59', '23:59']
    },
    {
        name: 'Social Corner Yaletown',
        safeword: '',
        alternativeExit: '',
        imgSrc: 'https://pub-0c6f5b8a360c417e9ac2670079e7c2c9.r2.dev/scy_marker1.png',
        lng: -123.11804358002048,
        lat: 49.278864165446805,
        open: ['00:00', '00:00', '00:00', '00:00', '00:00', '00:00', '00:00'],
        close: ['23:59', '23:59', '23:59', '23:59', '23:59', '23:59', '23:59']
    },
];

window.Business = Business;
window.PRECOOKED_BUSINESSES = PRECOOKED_BUSINESSES;
