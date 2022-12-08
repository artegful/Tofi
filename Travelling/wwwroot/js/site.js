
const input = document.getElementById("pac-input");
const options = {
    fields: ["name"],
    types: ["locality"]
};
const autocomplete = new google.maps.places.Autocomplete(input, options);

const departureInput = document.getElementById("departure-input");
const departureOptions = {
    fields: ["name"],
    types: ["locality"]
};
const departureAutocomplete = new google.maps.places.Autocomplete(departureInput, departureOptions);

const arriveInput = document.getElementById("arrive-input");
const arriveOptions = {
    fields: ["name"],
    types: ["locality"]
};
const arriveAutocomplete = new google.maps.places.Autocomplete(arriveInput, arriveOptions);