alert("Ever ?? Loaded in the file");
class Business {
    constructor() {
        this.name = 'vcc';
        //alert("Loaded in it");
        // this.safeword = safeword;
        // this.alternativeExit = alternativeExit;
    }

    displayInfo() {
        return this.name;
    }
}

//window.Business = Business;
export { Business };